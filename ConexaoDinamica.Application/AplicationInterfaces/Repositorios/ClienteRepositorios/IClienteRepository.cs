using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Entidades.Clientes;

namespace ConexaoDinamica.Application.AplicationInterfaces.Repositorios.ClienteRepositorios
{
    public interface IClienteRepository
    {
        /// <summary>
        /// Lista paginada, com busca livre por nome, documento ou e-mail.
        /// </summary>
        Task<ResultadoPaginado<Cliente>> ListarAsync(
            string? busca,
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default);

        Task<Cliente?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Documento é único; usado para recusar duplicidade antes de gravar.</summary>
        Task<bool> DocumentoEmUsoAsync(string documento, int? ignorarId = null, CancellationToken cancellationToken = default);

        Task<Cliente> CriarAsync(Cliente cliente, CancellationToken cancellationToken = default);

        Task AtualizarAsync(Cliente cliente, CancellationToken cancellationToken = default);

        Task RemoverAsync(Cliente cliente, CancellationToken cancellationToken = default);

        /// <summary>
        /// Indica se o cliente tem pedidos. A remoção é bloqueada nesse caso —
        /// a FK usa Restrict justamente para preservar o histórico.
        /// </summary>
        Task<bool> PossuiPedidosAsync(int clienteId, CancellationToken cancellationToken = default);
    }
}
