namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Dados que o formulário do AdminCenter envia para configurar o Postgres.
    ///
    /// Campos separados em vez de uma connection string pronta por dois motivos:
    /// o formulário precisa exibir cada campo isoladamente, e montar a string no
    /// servidor (com NpgsqlConnectionStringBuilder) garante o escaping correto —
    /// uma senha contendo ';' ou '=' corromperia a string se viesse concatenada.
    /// </summary>
    public class ConexaoPostgresRequest
    {
        public string Host { get; set; } = "localhost";

        public int Porta { get; set; } = 5432;

        public string Database { get; set; } = string.Empty;

        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Trafega em texto no corpo da requisição (por isso HTTPS é obrigatório
        /// em qualquer uso real). Nunca retorna em nenhuma resposta.
        /// </summary>
        public string Senha { get; set; } = string.Empty;
    }
}
