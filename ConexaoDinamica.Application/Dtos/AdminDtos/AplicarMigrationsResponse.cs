namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Resultado da aplicação de migrations no banco configurado.
    /// </summary>
    public class AplicarMigrationsResponse
    {
        public bool Sucesso { get; set; }

        public string Mensagem { get; set; } = string.Empty;

        /// <summary>
        /// Migrations aplicadas nesta execução. Vem vazio quando o banco já
        /// estava atualizado — o que é sucesso, e não falha.
        /// </summary>
        public IReadOnlyList<string> Aplicadas { get; set; } = [];

        /// <summary>
        /// Preenchido apenas quando o super administrador acabou de ser criado.
        /// Nas execuções seguintes vem nulo, porque o seed é idempotente.
        /// </summary>
        public SuperUsuarioCriadoResponse? SuperUsuario { get; set; }
    }

    /// <summary>
    /// Credenciais do super administrador recém-criado.
    ///
    /// A senha é gerada pelo servidor e aparece AQUI UMA ÚNICA VEZ: no banco fica
    /// apenas o hash BCrypt, e não há como recuperá-la depois. Isso é preferível a
    /// uma senha padrão fixa no código — que seria idêntica em toda instalação e
    /// acabaria esquecida, ativa e conhecida por qualquer um com acesso ao
    /// repositório.
    /// </summary>
    public class SuperUsuarioCriadoResponse
    {
        public string Email { get; set; } = string.Empty;
        public string SenhaProvisoria { get; set; } = string.Empty;
        public string Aviso { get; set; } = string.Empty;
    }
}
