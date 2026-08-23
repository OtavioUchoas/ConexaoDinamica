namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    public class AdminLoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
    }
}
