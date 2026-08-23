using ConexaoDinamica.Domain.Enums;

namespace ConexaoDinamica.Domain.Entidades.Usuarios
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Comum;
        public DateTime DataCriacao { get; set; }
    }
}
