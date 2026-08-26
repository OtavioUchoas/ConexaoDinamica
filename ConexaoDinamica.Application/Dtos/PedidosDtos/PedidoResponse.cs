namespace ConexaoDinamica.Application.Dtos.PedidosDtos
{
    public class PedidoResponse
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;

        public int ClienteId { get; set; }

        /// <summary>
        /// Nome do cliente junto do id, para a grid não precisar de uma consulta
        /// por linha só para exibir a quem o pedido pertence.
        /// </summary>
        public string ClienteNome { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime DataCriacao { get; set; }

        public List<ItemPedidoResponse> Itens { get; set; } = [];
    }

    public class ItemPedidoResponse
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }
}
