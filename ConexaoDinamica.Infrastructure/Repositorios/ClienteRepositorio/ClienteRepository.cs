using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.ClienteRepositorios;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Domain.Entidades.Clientes;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.EntityFrameworkCore;

namespace ConexaoDinamica.Infrastructure.Repositorios.ClienteRepositorio
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly AppDbContext _contexto;

        public ClienteRepository(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ResultadoPaginado<Cliente>> ListarAsync(
            string? busca,
            int pagina,
            int tamanhoPagina,
            CancellationToken cancellationToken = default)
        {
            var consulta = _contexto.Clientes.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = $"%{busca.Trim()}%";

                // EF.Functions.ILike é do Npgsql e faz comparação sem diferenciar
                // maiúsculas no próprio banco. Usar ToLower().Contains() em vez
                // disso impediria o uso de índice e traria a coluna inteira para
                // memória em tabelas grandes.
                consulta = consulta.Where(c =>
                    EF.Functions.ILike(c.Nome, termo) ||
                    EF.Functions.ILike(c.Documento, termo) ||
                    (c.Email != null && EF.Functions.ILike(c.Email, termo)));
            }

            var total = await consulta.CountAsync(cancellationToken);

            var itens = await consulta
                .OrderBy(c => c.Nome)
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .ToListAsync(cancellationToken);

            return new ResultadoPaginado<Cliente>
            {
                Itens = itens,
                Total = total,
                Pagina = pagina,
                TamanhoPagina = tamanhoPagina,
            };
        }

        /// <remarks>
        /// COM rastreamento, ao contrário da listagem: o resultado costuma ser
        /// alterado ou removido em seguida, e uma entidade sem rastreamento não
        /// produziria diff de auditoria — o interceptor não teria os valores
        /// originais para comparar.
        /// </remarks>
        public Task<Cliente?> ObterPorIdAsync(int id, CancellationToken cancellationToken = default) =>
            _contexto.Clientes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        public Task<bool> DocumentoEmUsoAsync(
            string documento,
            int? ignorarId = null,
            CancellationToken cancellationToken = default) =>
            _contexto.Clientes
                .AsNoTracking()
                // Na edição, o próprio registro precisa ser excluído da checagem,
                // senão salvar sem alterar o documento acusaria duplicidade.
                .AnyAsync(c => c.Documento == documento && (ignorarId == null || c.Id != ignorarId), cancellationToken);

        public async Task<Cliente> CriarAsync(Cliente cliente, CancellationToken cancellationToken = default)
        {
            _contexto.Clientes.Add(cliente);
            await _contexto.SaveChangesAsync(cancellationToken);
            return cliente;
        }

        public Task AtualizarAsync(Cliente cliente, CancellationToken cancellationToken = default) =>
            _contexto.SaveChangesAsync(cancellationToken);

        public Task RemoverAsync(Cliente cliente, CancellationToken cancellationToken = default)
        {
            _contexto.Clientes.Remove(cliente);
            return _contexto.SaveChangesAsync(cancellationToken);
        }

        public Task<bool> PossuiPedidosAsync(int clienteId, CancellationToken cancellationToken = default) =>
            _contexto.Pedidos.AsNoTracking().AnyAsync(p => p.ClienteId == clienteId, cancellationToken);
    }
}
