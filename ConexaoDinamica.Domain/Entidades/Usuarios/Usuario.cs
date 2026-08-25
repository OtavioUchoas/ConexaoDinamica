using ConexaoDinamica.Domain.Auditoria;
using ConexaoDinamica.Domain.Enums;

namespace ConexaoDinamica.Domain.Entidades.Usuarios
{
    public class Usuario : IAuditavelRaiz
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Fora da auditoria: sem o atributo, o snapshot levaria o hash para o
        /// Mongo e a trilha viraria uma cópia paralela das senhas do sistema.
        /// </summary>
        [NaoAuditar]
        public string SenhaHash { get; set; } = string.Empty;

        public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Comum;
        public DateTime DataCriacao { get; set; }
    }
}
