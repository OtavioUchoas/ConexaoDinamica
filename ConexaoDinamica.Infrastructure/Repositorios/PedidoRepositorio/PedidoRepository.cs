using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.PedidoRepositorios;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Entidades.Pedidos;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.EntityFrameworkCore;

namespace ConexaoDinamica.Infrastructure.Repositorios.PedidoRepositorio
{
    public class PedidoRepository : IPedidoRepository
    {
        private readonly AppDbContext _contexto;

        public PedidoRepository(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ResultadoPaginado<Pedido>> ListarAsync(
            string? busca,
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default)
        {
            // Include do Cliente porque a grid mostra o nome dele. Sem isso seria
            // uma consulta por linha — o clássico problema N+1.
            //
            // Os Itens NÃO são incluídos: a listagem só exibe o total, e trazer
            // todos os itens de todos os pedidos multiplicaria o volume à toa.
            var consulta = _contexto.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente);

            var filtrada = string.IsNullOrWhiteSpace(busca)
                ? consulta
                : consulta.Where(p =>
                    EF.Functions.ILike(p.Numero, $"%{busca.Trim()}%") ||
                    (p.Cliente != null && EF.Functions.ILike(p.Cliente.Nome, $"%{busca.Trim()}%")));

            var total = await filtrada.CountAsync(cancellationToken);

            var itens = await filtrada
                .OrderByDescending(p => p.DataCriacao)
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync(cancellationToken);

            return new ResultadoPaginado<Pedido>
            {
                Itens = itens,
                Total = total,
                Pagina = pagina,
                TamanhoPagina = tamanhoPagina,
            };
        }

        /// <remarks>
        /// COM rastreamento e COM os itens: este é o caminho da edição, e o
        /// interceptor precisa das entidades rastreadas para montar o diff — tanto
        /// do pedido quanto dos itens que entram no evento dele.
        /// </remarks>
        public Task<Pedido?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
            _contexto.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Itens)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public Task<bool> NumeroEmUsoAsync(
            string numero,
            int? ignorarId = null,
            CancellationToken cancellationToken = default) =>
            _contexto.Pedidos
                .AsNoTracking()
                .AnyAsync(p => p.Numero == numero && (ignorarId == null || p.Id != ignorarId), cancellationToken);

        public Task<bool> ClienteExisteAsync(int clienteId, CancellationToken cancellationToken = default) =>
            _contexto.Clientes.AsNoTracking().AnyAsync(c => c.Id == clienteId, cancellationToken);

        public async Task<Pedido> CriarAsync(Pedido pedido, CancellationToken cancellationToken = default)
        {
            _contexto.Pedidos.Add(pedido);
            await _contexto.SaveChangesAsync(cancellationToken);
            return pedido;
        }

        public Task AtualizarAsync(Pedido pedido, CancellationToken cancellationToken = default) =>
            _contexto.SaveChangesAsync(cancellationToken);

        public Task RemoverAsync(Pedido pedido, CancellationToken cancellationToken = default)
        {
            // Os itens somem junto pelo Cascade configurado no mapeamento.
            _contexto.Pedidos.Remove(pedido);
            return _contexto.SaveChangesAsync(cancellationToken);
        }

        public void RemoverItem(ItemPedido item) => _contexto.Remove(item);
    }
}
