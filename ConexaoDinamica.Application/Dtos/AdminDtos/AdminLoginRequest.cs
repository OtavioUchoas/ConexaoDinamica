namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    public class AdminLoginRequest
    {
        /// <summary>
        /// Username ou email do administrador.
        /// </summary>
        public string Login { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }
}
