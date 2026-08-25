using System.ComponentModel.DataAnnotations;

namespace ConexaoDinamica.Domain.Entidades.Pedidos
{
    public enum StatusPedido
    {
        [Display(Name = "Rascunho")]
        Rascunho = 1,

        [Display(Name = "Confirmado")]
        Confirmado = 2,

        [Display(Name = "Enviado")]
        Enviado = 3,

        [Display(Name = "Cancelado")]
        Cancelado = 4
    }
}
