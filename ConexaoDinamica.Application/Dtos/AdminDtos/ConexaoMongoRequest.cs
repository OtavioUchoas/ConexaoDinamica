namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Dados que o formulário do AdminCenter envia para configurar o MongoDB.
    /// </summary>
    public class ConexaoMongoRequest
    {
        public string Host { get; set; } = "localhost";

        public int Porta { get; set; } = 27017;

        public string Database { get; set; } = string.Empty;

        /// <summary>Opcional — vazio conecta sem autenticação.</summary>
        public string Usuario { get; set; } = string.Empty;

        /// <summary>Nunca retorna em nenhuma resposta.</summary>
        public string Senha { get; set; } = string.Empty;

        /// <summary>Banco onde as credenciais estão cadastradas. Normalmente "admin".</summary>
        public string AuthSource { get; set; } = "admin";
    }
}
