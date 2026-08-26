using ConexaoDinamica.Domain.Entidades.Pedidos;

namespace ConexaoDinamica.Application.Dtos.PedidosDtos
{
    public class PedidoRequest
    {
        public string Numero { get; set; } = string.Empty;
        public int ClienteId { get; set; }
        public StatusPedido Status { get; set; } = StatusPedido.Rascunho;

        /// <summary>
        /// Itens do pedido. Não há Total aqui de propósito: aceitar o valor
        /// calculado pelo cliente permitiria enviar um pedido de mil reais com
        /// total zero. O servidor recalcula a partir dos itens.
        /// </summary>
        public List<ItemPedidoRequest> Itens { get; set; } = [];
    }

    public class ItemPedidoRequest
    {
        /// <summary>
        /// Nulo para item novo; preenchido para item existente.
        ///
        /// É o que permite ATUALIZAR o item em vez de apagar e recriar — e isso
        /// muda a auditoria: recriar geraria remoção + adição a cada gravação,
        /// perdendo o histórico real da alteração ("Quantidade: 2 -> 5").
        /// </summary>
        public int? Id { get; set; }

        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
    }
}
