namespace ConexaoDinamica.Application.Configuracoes
{
    /// <summary>
    /// Dados de conexão do MongoDB, usado para os logs de auditoria.
    /// Mesmo desenho do <see cref="ConexaoPostgresConfig"/>: campos separados,
    /// configurados em runtime pelo AdminCenter e persistidos no LiteDB.
    /// </summary>
    public class ConexaoMongoConfig
    {
        /// <summary>Documento único no LiteDB — sempre 1.</summary>
        public int Id { get; set; } = 1;

        public string Host { get; set; } = "localhost";
        public int Porta { get; set; } = 27017;
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Opcional: instalações locais de Mongo costumam rodar sem autenticação.
        /// Vazio aqui significa conectar anonimamente.
        /// </summary>
        public string Usuario { get; set; } = string.Empty;

        /// <summary>Texto puro, mesma decisão tomada para o Postgres.</summary>
        public string Senha { get; set; } = string.Empty;

        /// <summary>
        /// Banco onde as credenciais estão cadastradas. No Mongo o usuário não
        /// vive necessariamente no banco que ele acessa — normalmente fica em
        /// "admin". Errar isso produz falha de autenticação mesmo com usuário e
        /// senha corretos, que é uma das confusões mais comuns do driver.
        /// Só é usado quando há usuário informado.
        /// </summary>
        public string AuthSource { get; set; } = "admin";

        public DateTime DataAtualizacao { get; set; }

        /// <summary>
        /// Usuário e senha não entram aqui de propósito: conexão sem autenticação
        /// é um cenário válido e comum em ambiente local.
        /// </summary>
        public bool EstaCompleta =>
            !string.IsNullOrWhiteSpace(Host) &&
            !string.IsNullOrWhiteSpace(Database) &&
            Porta > 0;

        /// <summary>
        /// Representação segura para log e debug — a senha nunca aparece.
        /// </summary>
        public override string ToString() =>
            $"{Host}:{Porta}/{Database} (usuario={(string.IsNullOrEmpty(Usuario) ? "<anonimo>" : Usuario)}, senha={(string.IsNullOrEmpty(Senha) ? "<vazia>" : "***")})";
    }
}
