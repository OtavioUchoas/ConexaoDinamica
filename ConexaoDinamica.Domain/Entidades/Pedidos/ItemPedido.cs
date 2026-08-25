using ConexaoDinamica.Domain.Auditoria;

namespace ConexaoDinamica.Domain.Entidades.Pedidos
{
    /// <summary>
    /// Parte do agregado Pedido: não possui trilha própria.
    ///
    /// Alterar a quantidade de um item é, para a auditoria, uma alteração DO
    /// PEDIDO — e aparece no evento dele como "Itens[3].Quantidade: 2 -> 5".
    /// </summary>
    public class ItemPedido : IAuditavelComoParte
    {
        public int Id { get; set; }

        public int PedidoId { get; set; }
        public Pedido? Pedido { get; set; }

        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
    }
}
