using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoDinamica.API.Controllers.AdminController
{
    /// <summary>
    /// Consulta da trilha de auditoria.
    ///
    /// ── Por que fica sob /admin e exige role Administrador ────────────────────
    /// A trilha registra quem fez o quê, com snapshots dos dados alterados. Isso
    /// a torna, na prática, uma via alternativa de leitura de TODO o sistema:
    /// quem consulta a auditoria de um cliente vê os dados dele sem precisar de
    /// permissão sobre clientes.
    ///
    /// Por isso ela não é apenas "mais uma listagem" — é informação privilegiada,
    /// e o controle de acesso aqui precisa ser pelo menos tão restrito quanto o
    /// dos dados originais.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/auditoria")]
    [Authorize(Roles = "Administrador")]
    [Tags("Admin / Auditoria")]
    public class AdminAuditoriaController : ControllerBase
    {
        private readonly IAuditoriaRepository _auditoria;

        public AdminAuditoriaController(IAuditoriaRepository auditoria)
        {
            _auditoria = auditoria;
        }

        /// <summary>
        /// Consulta os eventos, do mais recente para o mais antigo.
        /// </summary>
        /// <remarks>
        /// Os filtros são opcionais e combináveis. Sem nenhum, devolve a primeira
        /// página dos eventos mais recentes.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ResultadoPaginado<EventoAuditoria>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Consultar(
            [FromQuery] string? tipoEntidade,
            [FromQuery] string? entidadeId,
            [FromQuery] TipoEventoAuditoria? tipoEvento,
            [FromQuery] string? usuarioId,
            [FromQuery] DateTime? dataInicio,
            [FromQuery] DateTime? dataFim,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanhoPagina = 25,
            CancellationToken cancellationToken = default)
        {
            var filtro = new FiltroAuditoria
            {
                TipoEntidade = tipoEntidade,
                EntidadeId = entidadeId,
                TipoEvento = tipoEvento,
                UsuarioId = usuarioId,
                DataInicio = dataInicio,
                DataFim = dataFim,
                Pagina = pagina,
                TamanhoPagina = tamanhoPagina,
            };

            try
            {
                return Ok(await _auditoria.ConsultarAsync(filtro, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                // MongoDB não configurado. 503 e não 500: é indisponibilidade de
                // dependência, e a mensagem diz o que fazer a respeito.
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "A trilha de auditoria está indisponível: o MongoDB não está configurado.",
                });
            }
        }

        /// <summary>
        /// Tipos de entidade presentes na trilha, para alimentar o filtro.
        /// </summary>
        /// <remarks>
        /// Vem do banco em vez de uma lista fixa: assim o filtro acompanha
        /// automaticamente qualquer entidade que passe a ser auditada.
        /// </remarks>
        [HttpGet("tipos-entidade")]
        [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ObterTiposEntidade(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _auditoria.ObterTiposEntidadeAsync(cancellationToken));
            }
            catch (InvalidOperationException)
            {
                // Lista vazia é resposta razoável aqui: este endpoint só alimenta
                // um filtro, e derrubar a tela inteira por causa dele seria
                // desproporcional.
                return Ok(Array.Empty<string>());
            }
        }
    }
}
