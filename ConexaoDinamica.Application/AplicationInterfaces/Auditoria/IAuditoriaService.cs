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
    }
}
