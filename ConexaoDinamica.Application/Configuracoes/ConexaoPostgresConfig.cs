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

        /// <summary>
        /// Representação segura para log, debug e mensagens de erro: a senha nunca
        /// aparece. ToString() é chamado implicitamente por logging estruturado e
        /// interpolação de string, então montar a connection string aqui vazaria a
        /// credencial sem ninguém perceber.
        /// A montagem fica na camada de infraestrutura, que conhece o Npgsql.
        /// </summary>
        public override string ToString() =>
            $"{Host}:{Porta}/{Database} (usuario={Usuario}, senha={(string.IsNullOrEmpty(Senha) ? "<vazia>" : "***")})";
    }
}
