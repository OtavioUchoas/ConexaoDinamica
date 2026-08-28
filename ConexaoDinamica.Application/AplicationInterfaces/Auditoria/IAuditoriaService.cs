using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Application.AplicationInterfaces.Auditoria
{
    /// <summary>
    /// Registro explícito de eventos de auditoria, para o que o interceptor do EF
    /// não tem como capturar.
    ///
    /// ── Por que visualização não pode ser automática ──────────────────────────
    /// O interceptor só é acionado por SaveChanges. Uma consulta não altera nada,
    /// não passa por ali, e portanto é invisível para ele — a chamada precisa
    /// partir de quem exibiu o dado.
    ///
    /// ── Quando chamar ────────────────────────────────────────────────────────
    /// Ao abrir o detalhe de um registro individual, não em listagens. A diferença
    /// importa por dois motivos:
    ///
    ///   Volume    — leituras superam escritas por ordens de grandeza, e uma
    ///               listagem paginada geraria centenas de eventos por tela.
    ///   Semântica — "acessou o cliente 42" é um fato auditável; "viu uma lista
    ///               onde 42 aparecia numa linha" é ruído: pode nem ter olhado.
    ///
    /// Para exportações e relatórios, registre o CRITÉRIO da consulta
    /// ("exportou clientes da cidade X, 340 registros"), nunca um evento por
    /// registro retornado.
    /// </summary>
    public interface IAuditoriaService
    {
        /// <summary>
        /// Registra que um usuário acessou o detalhe de um registro.
        /// </summary>
        /// <param name="tipoEntidade">Nome da entidade. Sem ele, um id solto é ambíguo: "42" de quê?</param>
        /// <param name="entidadeId">Identificador do registro acessado.</param>
        Task RegistrarVisualizacaoAsync(
            string tipoEntidade,
            string entidadeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra que a trilha foi exportada para fora do sistema.
        ///
        /// É o evento mais importante desta interface. Exportar a auditoria é o
        /// gesto que tira os dados de dentro de qualquer controle de acesso: a
        /// partir dali o arquivo circula por e-mail, pen drive e pasta
        /// compartilhada. Sem este registro, a única ação que realmente esvazia a
        /// trilha seria a única que ela não veria.
        ///
        /// Segue o critério documentado acima: grava o FILTRO e o volume, nunca um
        /// evento por linha exportada.
        /// </summary>
        /// <param name="criterio">Filtros aplicados, em texto legível.</param>
        /// <param name="quantidade">Número de eventos que saíram no arquivo.</param>
        Task RegistrarExportacaoAsync(
            string criterio,
            int quantidade,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra uma autenticação bem-sucedida.
        ///
        /// Quem autentica ainda não é o usuário da requisição — o token acabou de
        /// ser emitido e o HttpContext continua anônimo. Por isso a identidade vem
        /// por parâmetro em vez de sair do contexto, como acontece nos demais
        /// eventos.
        ///
        /// O cadastro NÃO passa por aqui: criar usuário grava uma linha no
        /// Postgres e o interceptor já o registra como Adicao de Usuario. Um
        /// evento a mais só duplicaria o mesmo fato.
        /// </summary>
        /// <param name="credencial">
        /// De onde veio a credencial: "Usuario" para conta do banco,
        /// "AdminBootstrap" para o administrador de configuração. Distingue duas
        /// portas de entrada com poderes bem diferentes.
        /// </param>
        /// <param name="identificador">Login ou e-mail informado.</param>
        /// <param name="usuario">Quem entrou, já resolvido.</param>
        Task RegistrarAutenticacaoAsync(
            string credencial,
            string identificador,
            UsuarioAuditado usuario,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra uma tentativa de autenticação recusada.
        ///
        /// O evento fica sem usuário — é o único da trilha em que isso é o
        /// esperado, e não uma falha de captura. O que identifica a tentativa é o
        /// par (identificador informado, origem).
        /// </summary>
        /// <param name="credencial">"Usuario" ou "AdminBootstrap".</param>
        /// <param name="identificador">Login ou e-mail informado na tentativa.</param>
        /// <param name="motivo">
        /// Por que foi recusada, no nível de detalhe que o chamador realmente tem.
        /// Não sirva aqui o que a resposta HTTP esconde de propósito: distinguir
        /// "conta não existe" de "senha errada" na trilha reintroduz, para quem
        /// tiver acesso a ela, a enumeração de contas que o login evita.
        /// </param>
        Task RegistrarFalhaAutenticacaoAsync(
            string credencial,
            string identificador,
            string motivo,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra uma mudança de infraestrutura: conexão de banco, migrations.
        ///
        /// A configuração vive no LiteDB local, fora de qualquer DbContext, então
        /// o interceptor nunca a vê. E são as ações de maior alcance do sistema:
        /// apontar o Postgres para outro servidor troca todos os dados que a
        /// aplicação enxerga sem alterar um único registro.
        ///
        /// ── Sobre a troca da conexão do Mongo ─────────────────────────────────
        /// O evento é gravado no banco que estiver valendo NO MOMENTO DA CHAMADA.
        /// Registrar depois de trocar grava no destino novo — que é o que se quer:
        /// a trilha nova começa dizendo de onde veio e quem a trouxe. A trilha
        /// antiga fica com uma lacuna no fim, e fechá-la exigiria gravar nos dois
        /// bancos; a continuidade para a frente vale mais.
        /// </summary>
        /// <param name="alvo">
        /// O que foi configurado — "ConexaoPostgres", "ConexaoMongo", "Migrations".
        /// Vira o identificador do registro na trilha.
        /// </param>
        /// <param name="detalhes">
        /// Para onde a aplicação passou a apontar. NUNCA inclua senha: a trilha
        /// costuma ter controle de acesso mais frouxo que a configuração em si, e
        /// não pode virar uma fonte alternativa de credenciais.
        /// </param>
        Task RegistrarConfiguracaoAsync(
            string alvo,
            IReadOnlyDictionary<string, object?> detalhes,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Registra que alguém consultou a própria trilha.
        ///
        /// ── Por que isto vale a exceção à regra ───────────────────────────────
        /// A regra documentada acima diz para não auditar listagem. Esta é a
        /// listagem que a regra não previa: a trilha é uma via alternativa de
        /// leitura de todo o sistema — quem consulta a auditoria de um cliente vê
        /// os dados dele sem ter permissão sobre clientes. Auditar a exportação e
        /// não a consulta deixava passar o mesmo acesso, só que mais devagar.
        ///
        /// O motivo de volume também não se aplica: a tela é restrita a
        /// administradores, então são dezenas de eventos por dia, não milhares.
        ///
        /// ── O ruído que isto cria, e o que fazer com ele ──────────────────────
        /// Consultar a trilha grava na trilha, então navegar por ela produz
        /// eventos que aparecem na navegação seguinte. É um efeito real e
        /// inevitável se a consulta vai ser auditada. Ele fica contido porque o
        /// evento usa o tipo "TrilhaAuditoria": filtrar por qualquer outra
        /// entidade já o remove da tela.
        ///
        /// Registra o CRITÉRIO e o volume, como a exportação — nunca os registros
        /// que apareceram.
        /// </summary>
        /// <param name="criterio">Filtros aplicados, em texto legível.</param>
        /// <param name="pagina">Página consultada.</param>
        /// <param name="totalEncontrado">Quantos eventos o filtro alcança, no total.</param>
        Task RegistrarConsultaTrilhaAsync(
            string criterio,
            int pagina,
            long totalEncontrado,
            CancellationToken cancellationToken = default);
    }
}
