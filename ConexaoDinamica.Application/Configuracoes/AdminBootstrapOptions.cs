namespace ConexaoDinamica.Application.Configuracoes
{
    /// <summary>
    /// Credenciais do administrador de bootstrap (break-glass).
    /// Vive fora do banco de propósito: é o acesso usado para configurar
    /// as conexões — e para recuperar o acesso caso o banco fique indisponível.
    /// </summary>
    public class AdminBootstrapOptions
    {
        public const string SectionName = "AdminBootstrap";

        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Nome { get; set; } = "Administrador";

        /// <summary>Hash BCrypt da senha. Nunca a senha em texto puro.</summary>
        public string SenhaHash { get; set; } = string.Empty;

        /// <summary>
        /// Sem hash configurado não há login possível — evita que uma seção
        /// ausente no appsettings vire um acesso liberado.
        /// </summary>
        public bool EstaConfigurado =>
            !string.IsNullOrWhiteSpace(SenhaHash) &&
            (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Email));
    }
}
