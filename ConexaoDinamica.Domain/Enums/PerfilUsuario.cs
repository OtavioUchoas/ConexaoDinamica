using System.ComponentModel.DataAnnotations;

namespace ConexaoDinamica.Domain.Enums
{
    public enum PerfilUsuario
    {
        [Display(Name = "Usuário comum")]
        Comum = 1,

        [Display(Name = "Administrador")]
        Administrador = 2
    }
}
