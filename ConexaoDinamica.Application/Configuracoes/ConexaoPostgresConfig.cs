namespace ConexaoDinamica.Application.Configuracoes
{
    /// <summary>
    /// Dados de conexão do Postgres, configurados em runtime pelo AdminCenter.
    /// Não é entidade de domínio: é detalhe de infraestrutura, sem regra de negócio.
    /// Guardamos os campos separados (e não a connection string pronta) para o
    /// AdminCenter conseguir reexibir o formulário sem precisar parsear a string.
    /// </summary>
    public class ConexaoPostgresConfig
    {
        /// <summary>Documento único no LiteDB — sempre 1.</summary>
        public int Id { get; set; } = 1;

        public string Host { get; set; } = "localhost";
        public int Porta { get; set; } = 5432;
        public string Database { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Texto puro por ora (decisão consciente para ambiente local de estudo).
        /// Para proteger depois, criptografe na borda do store — o contrato não muda.
        /// </summary>
        public string Senha { get; set; } = string.Empty;

        public DateTime DataAtualizacao { get; set; }

        public bool EstaCompleta =>
            !string.IsNullOrWhiteSpace(Host) &&
            !string.IsNullOrWhiteSpace(Database) &&
            !string.IsNullOrWhiteSpace(Usuario) &&
            Porta > 0;
    }
}
