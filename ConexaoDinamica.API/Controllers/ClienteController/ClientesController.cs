using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.ClienteRepositorios;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Application.Dtos.ClientesDtos;
using ConexaoDinamica.Domain.Entidades.Clientes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoDinamica.API.Controllers.ClienteController
{
    /// <summary>
    /// CRUD de clientes.
    ///
    /// Criação, alteração e remoção NÃO chamam auditoria explicitamente: o
    /// SaveChangesInterceptor as captura porque Cliente é IAuditavelRaiz. Só a
    /// visualização precisa de chamada própria — uma consulta não passa por
    /// SaveChanges, então o interceptor não tem como enxergá-la.
    /// </summary>
    [ApiController]
    [Route("api/v1/clientes")]
    [Authorize]
    [Tags("Clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteRepository _repositorio;
        private readonly IAuditoriaService _auditoria;

        public ClientesController(IClienteRepository repositorio, IAuditoriaService auditoria)
        {
            _repositorio = repositorio;
            _auditoria = auditoria;
        }

        /// <summary>Lista paginada, com busca por nome, documento ou e-mail.</summary>
        /// <remarks>
        /// Listagem não gera evento de auditoria de propósito: leituras superam
        /// escritas por ordens de grandeza, e "apareceu numa lista" não é um fato
        /// auditável — o usuário pode nem ter olhado.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ResultadoPaginado<ClienteResponse>), StatusCodes.Status200OK)]
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

            return Ok(new ResultadoPaginado<ClienteResponse>
            {
                Itens = resultado.Itens.Select(Mapear).ToList(),
                Total = resultado.Total,
                Pagina = resultado.Pagina,
                TamanhoPagina = resultado.TamanhoPagina,
            });
        }

        /// <summary>Detalhe de um cliente. Registra evento de visualização.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObterPorId(int id, CancellationToken cancellationToken)
        {
            var cliente = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (cliente is null)
                return NotFound(new { message = "Cliente não encontrado." });

            // Só depois de confirmar que existe: auditar antes marcaria como
            // visualizado um id inexistente, permitindo poluir a trilha por
            // tentativa e erro.
            await _auditoria.RegistrarVisualizacaoAsync(nameof(Cliente), id.ToString(), cancellationToken);

            return Ok(Mapear(cliente));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Criar(
            [FromBody] ClienteRequest request,
            CancellationToken cancellationToken)
        {
            if (await _repositorio.DocumentoEmUsoAsync(request.Documento, null, cancellationToken))
            {
                // 409 e não 400: os dados são válidos, o conflito é com o estado
                // atual do sistema.
                return Conflict(new { message = "Já existe um cliente com este documento." });
            }

            var cliente = await _repositorio.CriarAsync(new Cliente
            {
                Nome = request.Nome.Trim(),
                Documento = request.Documento.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                DataCadastro = DateTime.UtcNow,
            }, cancellationToken);

            return CreatedAtAction(nameof(ObterPorId), new { id = cliente.Id }, Mapear(cliente));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ClienteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Atualizar(
            int id,
            [FromBody] ClienteRequest request,
            CancellationToken cancellationToken)
        {
            var cliente = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (cliente is null)
                return NotFound(new { message = "Cliente não encontrado." });

            if (await _repositorio.DocumentoEmUsoAsync(request.Documento, id, cancellationToken))
                return Conflict(new { message = "Já existe outro cliente com este documento." });

            // As atribuições acontecem sobre a entidade RASTREADA. É isso que
            // permite ao interceptor comparar valores originais e atuais e montar
            // o diff da auditoria.
            cliente.Nome = request.Nome.Trim();
            cliente.Documento = request.Documento.Trim();
            cliente.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

            await _repositorio.AtualizarAsync(cliente, cancellationToken);

            return Ok(Mapear(cliente));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Remover(int id, CancellationToken cancellationToken)
        {
            var cliente = await _repositorio.ObterPorIdAsync(id, cancellationToken);

            if (cliente is null)
                return NotFound(new { message = "Cliente não encontrado." });

            if (await _repositorio.PossuiPedidosAsync(id, cancellationToken))
            {
                // Verificado aqui para responder com mensagem útil. Sem isso, a FK
                // com Restrict lançaria uma exceção de banco que viraria 500 pelo
                // middleware global — correta, porém incompreensível para quem usa.
                return Conflict(new
                {
                    message = "Este cliente possui pedidos e não pode ser removido.",
                });
            }

            await _repositorio.RemoverAsync(cliente, cancellationToken);

            return NoContent();
        }

        private static ClienteResponse Mapear(Cliente cliente) => new()
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            Documento = cliente.Documento,
            Email = cliente.Email,
            DataCadastro = cliente.DataCadastro,
        };
    }
}
