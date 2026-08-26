using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.PedidoRepositorios;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Application.Dtos.PedidosDtos;
using ConexaoDinamica.Domain.Entidades.Pedidos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoDinamica.API.Controllers.PedidoController
{
    /// <summary>
    /// CRUD de pedidos.
    ///
    /// Diferente de Cliente, aqui existe um agregado: o pedido é a raiz e os
    /// itens são partes dele. Isso aparece na auditoria — alterar o status e a
    /// quantidade de um item gera UM evento, com o diff apontando para dentro
    /// ("Itens[7].Quantidade: 2 -> 5"), e não dois eventos soltos.
    /// </summary>
    [ApiController]
    [Route("api/v1/pedidos")]
    [Authorize]
    [Tags("Pedidos")]
    public class PedidosController : ControllerBase
    {
        private readonly IPedidoRepository _repositorio;
        private readonly IAuditoriaService _auditoria;

        public PedidosController(IPedidoRepository repositorio, IAuditoriaService auditoria)
        {
            _repositorio = repositorio;
            _auditoria = auditoria;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResultadoPaginado<PedidoResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? busca,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanhoPagina = 10,
            CancellationToken cancellationToken = default)
        {
            var resultado = await _repositorio.ListarAsync(
                busca,
                Math.Max(1, pagina),
                Math.Clamp(tamanhoPagina, 1, 100),
                cancellationToken);

            return Ok(new ResultadoPaginado<PedidoResponse>
            {
                // Sem os itens: a grid mostra apenas o total, e carregá-los para
                // todas as linhas multiplicaria o volume sem necessidade.
                Itens = resultado.Itens.Select(p => Mapear(p, incluirItens: false)).ToList(),
                Total = resultado.Total,
                Pagina = resultado.Pagina,
                TamanhoPagina = resultado.TamanhoPagina,
            });
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PedidoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObterPorId(int id, CancellationToken cancellationToken)
        {
            var pedido = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (pedido is null)
                return NotFound(new { message = "Pedido não encontrado." });

            await _auditoria.RegistrarVisualizacaoAsync(nameof(Pedido), id.ToString(), cancellationToken);

            return Ok(Mapear(pedido, incluirItens: true));
        }

        [HttpPost]
        [ProducesResponseType(typeof(PedidoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Criar(
            [FromBody] PedidoRequest request,
            CancellationToken cancellationToken)
        {
            if (!await _repositorio.ClienteExisteAsync(request.ClienteId, cancellationToken))
                return BadRequest(new { message = "Cliente informado não existe." });

            if (await _repositorio.NumeroEmUsoAsync(request.Numero, null, cancellationToken))
                return Conflict(new { message = "Já existe um pedido com este número." });

            var pedido = new Pedido
            {
                Numero = request.Numero.Trim(),
                ClienteId = request.ClienteId,
                Status = request.Status,
                DataCriacao = DateTime.UtcNow,
                Itens = request.Itens.Select(i => new ItemPedido
                {
                    Descricao = i.Descricao.Trim(),
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario,
                }).ToList(),
            };

            pedido.Total = CalcularTotal(pedido.Itens);

            await _repositorio.CriarAsync(pedido, cancellationToken);

            var criado = await _repositorio.ObterPorIdAsync(pedido.Id, cancellationToken);

            return CreatedAtAction(nameof(ObterPorId), new { id = pedido.Id }, Mapear(criado!, true));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(PedidoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Atualizar(
            int id,
            [FromBody] PedidoRequest request,
            CancellationToken cancellationToken)
        {
            var pedido = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (pedido is null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (!await _repositorio.ClienteExisteAsync(request.ClienteId, cancellationToken))
                return BadRequest(new { message = "Cliente informado não existe." });

            if (await _repositorio.NumeroEmUsoAsync(request.Numero, id, cancellationToken))
                return Conflict(new { message = "Já existe outro pedido com este número." });

            pedido.Numero = request.Numero.Trim();
            pedido.ClienteId = request.ClienteId;
            pedido.Status = request.Status;

            SincronizarItens(pedido, request.Itens);

            pedido.Total = CalcularTotal(pedido.Itens);

            await _repositorio.AtualizarAsync(pedido, cancellationToken);

            var atualizado = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            return Ok(Mapear(atualizado!, true));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
        {
            var pedido = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (pedido is null)
                return NotFound(new { message = "Pedido não encontrado." });

            await _repositorio.RemoverAsync(pedido, cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Reconcilia os itens enviados com os já existentes.
        ///
        /// ── Por que não apagar tudo e recriar ─────────────────────────────────
        /// Seria bem mais simples, e produziria uma auditoria inútil: toda
        /// gravação registraria a remoção de todos os itens seguida da adição de
        /// todos de novo. Alterar a quantidade de um item viraria "removidos 3,
        /// adicionados 3" em vez de "Itens[7].Quantidade: 2 -> 5".
        ///
        /// Além disso, recriar troca os ids a cada gravação, e a trilha perderia
        /// a capacidade de acompanhar um item específico ao longo do tempo.
        ///
        /// Por isso: item com id conhecido é ATUALIZADO, item sem id é ADICIONADO,
        /// e item ausente da requisição é REMOVIDO.
        /// </summary>
        private void SincronizarItens(Pedido pedido, List<ItemPedidoRequest> enviados)
        {
            var idsEnviados = enviados.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();

            // ToList() antes de iterar: a coleção é modificada dentro do laço.
            foreach (var existente in pedido.Itens.ToList())
            {
                if (idsEnviados.Contains(existente.Id))
                    continue;

                pedido.Itens.Remove(existente);
                _repositorio.RemoverItem(existente);
            }

            foreach (var enviado in enviados)
            {
                if (enviado.Id.HasValue)
                {
                    var existente = pedido.Itens.FirstOrDefault(i => i.Id == enviado.Id.Value);

                    // Id que não pertence a este pedido é ignorado: aceitá-lo
                    // permitiria mover um item de um pedido para outro por engano.
                    if (existente is null)
                        continue;

                    existente.Descricao = enviado.Descricao.Trim();
                    existente.Quantidade = enviado.Quantidade;
                    existente.PrecoUnitario = enviado.PrecoUnitario;
                }
                else
                {
                    pedido.Itens.Add(new ItemPedido
                    {
                        Descricao = enviado.Descricao.Trim(),
                        Quantidade = enviado.Quantidade,
                        PrecoUnitario = enviado.PrecoUnitario,
                    });
                }
            }
        }

        /// <summary>
        /// Calculado no servidor, sempre. Aceitar o total vindo do cliente
        /// permitiria registrar um pedido de mil reais com total zero.
        /// </summary>
        private static decimal CalcularTotal(IEnumerable<ItemPedido> itens) =>
            itens.Sum(i => i.Quantidade * i.PrecoUnitario);

        private static PedidoResponse Mapear(Pedido pedido, bool incluirItens) => new()
        {
            Id = pedido.Id,
            Numero = pedido.Numero,
            ClienteId = pedido.ClienteId,
            ClienteNome = pedido.Cliente?.Nome ?? string.Empty,
            Status = pedido.Status.ToString(),
            Total = pedido.Total,
            DataCriacao = pedido.DataCriacao,
            Itens = incluirItens
                ? pedido.Itens.Select(i => new ItemPedidoResponse
                {
                    Id = i.Id,
                    Descricao = i.Descricao,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario,
                }).ToList()
                : [],
        };
    }
}
