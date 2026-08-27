namespace ConexaoDinamica.Application.Auditoria
{
    /// <summary>
    /// Um registro da trilha de auditoria, gravado no MongoDB.
    ///
    /// Não depende do driver do Mongo de propósito: o Id é um Guid gerado pela
    /// aplicação em vez de ObjectId, o que evita trazer MongoDB.Bson para a
    /// Application. O driver mapeia a propriedade "Id" para "_id" por convenção.
    ///
    /// O que cada tipo de evento preenche:
    ///
    ///   Adicao       -> Snapshot (estado inicial), sem Alteracoes
    ///   Alteracao    -> Alteracoes (o que mudou) + Snapshot (estado final)
    ///   Remocao      -> Snapshot (último estado antes de sumir), sem Alteracoes
    ///   Visualizacao -> nem um nem outro; basta quem acessou o quê
    ///
    /// O par Alteracoes + Snapshot é redundante de propósito. São leituras
    /// diferentes: o diff é para humano ("Status: Ativo -> Inativo"), o snapshot
    /// é âncora para reconstruir o estado sem replay da cadeia inteira. Se um
    /// evento se perder, uma trilha só de diffs quebra silenciosamente; com
    /// snapshot, cada evento se sustenta sozinho.
    /// </summary>
    public class EventoAuditoria
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// O formato deste documento vai mudar. Guardar a versão custa nada agora
        /// e evita ficar incapaz de ler os registros antigos depois.
        /// </summary>
        public int VersaoSchema { get; set; } = 1;

        public DateTime DataHora { get; set; } = DateTime.UtcNow;

        public TipoEventoAuditoria TipoEvento { get; set; }

        /// <summary>
        /// Agrupa eventos de uma mesma operação de negócio. Uma única ação costuma
        /// alterar várias entidades; sem isto, a trilha vira eventos soltos sem
        /// como reconstruir "essa transação fez isso, isso e aquilo".
        /// </summary>
        public string? CorrelationId { get; set; }

        public EntidadeAuditada Entidade { get; set; } = new();

        public UsuarioAuditado? Usuario { get; set; }

        public OrigemAuditada? Origem { get; set; }

        public List<AlteracaoCampo> Alteracoes { get; set; } = [];

        /// <summary>
        /// Estado da entidade, montado a partir do ChangeTracker do EF e não por
        /// serialização do objeto. A diferença é decisiva: serializar seguiria as
        /// navegações, puxando a árvore inteira de relacionamentos, entrando em
        /// loop nas referências circulares e disparando lazy loading de dentro do
        /// interceptor. Vindo do ChangeTracker, só existem campos escalares —
        /// a árvore sequer é alcançável.
        /// </summary>
        public Dictionary<string, object?> Snapshot { get; set; } = [];

        /// <summary>
        /// Chaves estrangeiras resolvidas com descrição legível, declaradas via
        /// [AuditarReferencia]. Ficam em campo próprio, tipado, e não dentro do
        /// Snapshot: como o snapshot guarda object, o driver do Mongo precisaria
        /// gravar um discriminador de tipo junto de cada objeto complexo
        /// (_t: "System.Collections.Generic.Dictionary..."), poluindo o documento
        /// e atrapalhando consultas.
        /// </summary>
        public Dictionary<string, ReferenciaAuditada> Referencias { get; set; } = [];

        /// <summary>
        /// Partes do agregado (itens de um pedido, por exemplo), agrupadas pelo nome
        /// da coleção. Ficam em campo tipado, e não dentro do Snapshot, pelo mesmo
        /// motivo das Referencias: uma lista guardada como object faria o driver
        /// gravar o discriminador de tipo junto
        /// (_t: "System.Collections.Generic.List..."), poluindo o documento.
        ///
        /// Só a raiz do agregado tem evento próprio; as partes vivem aqui dentro.
        /// </summary>
        public Dictionary<string, List<Dictionary<string, object?>>> Partes { get; set; } = [];

        /// <summary>
        /// Partes que SAÍRAM do agregado nesta operação, com o último estado que
        /// tinham antes de sumir.
        ///
        /// ── Por que não basta o diff ─────────────────────────────────────────
        /// A saída de uma parte é registrada em Alteracoes como
        /// "Itens[15]: existente -> removido", e "existente" ali é um texto fixo,
        /// não um valor. Sem este campo, a descrição, a quantidade e o preço do
        /// item apagado não sobreviviam em lugar nenhum do evento: para saber o
        /// que se perdeu era preciso caçar um evento anterior do mesmo pedido e
        /// torcer para que ele ainda existisse.
        ///
        /// Isso contrariava a própria política da trilha — adição e remoção
        /// guardam o estado completo. Valia para a raiz e não valia para as partes.
        ///
        /// Fica separado de Partes, e não misturado com uma flag, porque as duas
        /// respondem perguntas diferentes: Partes é "como está", esta é "o que
        /// deixou de estar". Juntá-las obrigaria todo leitor a filtrar antes de
        /// entender o estado atual.
        /// </summary>
        public Dictionary<string, List<Dictionary<string, object?>>> PartesRemovidas { get; set; } = [];
    }

    /// <summary>Qual entidade o evento descreve.</summary>
    public class EntidadeAuditada
    {
        /// <summary>Sem o tipo, um id solto é ambíguo: "42" de quê?</summary>
        public string Tipo { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// Quem fez. O nome e o email são desnormalizados no momento do evento pelo
    /// mesmo motivo das entidades relacionadas: "usuarioId: 3" não diz nada daqui
    /// a um ano, se o usuário tiver sido renomeado ou removido.
    /// </summary>
    public class UsuarioAuditado
    {
        public string Id { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class OrigemAuditada
    {
        public string? Ip { get; set; }
        public string? UserAgent { get; set; }
    }

    /// <summary>
    /// Referência a outro agregado, preservada com o significado que tinha no
    /// momento do evento. O Id mantém a rastreabilidade; a Descricao mantém o
    /// sentido, mesmo que a entidade seja renomeada ou removida depois.
    /// </summary>
    public class ReferenciaAuditada
    {
        public string Id { get; set; } = string.Empty;
        public string? Descricao { get; set; }
    }

    public class AlteracaoCampo
    {
        public string Campo { get; set; } = string.Empty;
        public object? De { get; set; }
        public object? Para { get; set; }
    }
}
