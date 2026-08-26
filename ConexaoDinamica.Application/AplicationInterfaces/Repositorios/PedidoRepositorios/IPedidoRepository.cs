using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Entidades.Pedidos;

namespace ConexaoDinamica.Application.AplicationInterfaces.Repositorios.PedidoRepositorios
{
    public interface IPedidoRepository
    {
        /// <summary>
        /// Lista paginada, com busca por número do pedido ou nome do cliente.
        /// Traz o cliente junto; os itens ficam de fora por não aparecerem na grid.
        /// </summary>
        Task<ResultadoPaginado<Pedido>> ListarAsync(
            string? busca,
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default);

        /// <summary>Traz o pedido com cliente e itens, rastreado para edição.</summary>
        Task<Pedido?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

        Task<bool> NumeroEmUsoAsync(string numero, int? ignorarId = null, CancellationToken cancellationToken = default);

        Task<bool> ClienteExisteAsync(int clienteId, CancellationToken cancellationToken = default);

        Task<Pedido> CriarAsync(Pedido pedido, CancellationToken cancellationToken = default);

        Task AtualizarAsync(Pedido pedido, CancellationToken cancellationToken = default);

        Task RemoverAsync(Pedido pedido, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marca um item para exclusão.
        ///
        /// Necessário porque remover da coleção em memória não basta: sem isto o
        /// EF apenas desassociaria o item, e a chave estrangeira obrigatória
        /// faria a gravação falhar.
        /// </summary>
        void RemoverItem(ItemPedido item);
    }
}
