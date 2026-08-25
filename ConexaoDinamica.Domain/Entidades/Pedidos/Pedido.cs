using ConexaoDinamica.Domain.Auditoria;
using ConexaoDinamica.Domain.Entidades.Clientes;

namespace ConexaoDinamica.Domain.Entidades.Pedidos
{
    /// <summary>
    /// Agregado raiz. Os itens fazem parte dele; o cliente não.
    /// </summary>
    public class Pedido : IAuditavelRaiz
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;

        /// <summary>
        /// Referência a outro agregado. O atributo faz a auditoria gravar
        /// { id, descricao } em vez de apenas o número, preservando quem era o
        /// cliente no momento do evento.
        /// </summary>
        [AuditarReferencia(typeof(Cliente), nameof(Clientes.Cliente.Nome))]
        public int ClienteId { get; set; }

        public Cliente? Cliente { get; set; }

        public StatusPedido Status { get; set; } = StatusPedido.Rascunho;
        public decimal Total { get; set; }
        public DateTime DataCriacao { get; set; }

        /// <summary>
        /// Parte do agregado: os itens são auditados dentro do evento do pedido.
        /// </summary>
        public List<ItemPedido> Itens { get; set; } = [];
    }
}
