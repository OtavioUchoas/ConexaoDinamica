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
    ///
    /// ── Por que a consulta também é auditada ──────────────────────────────────
    /// Pela mesma razão: se ler a trilha equivale a ler o sistema inteiro, então
    /// registrar só a exportação deixava passar exatamente o mesmo acesso, feito
    /// pela tela em vez do arquivo. Consultar aqui gera um evento em
    /// "TrilhaAuditoria", como já acontecia ao exportar.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/auditoria")]
    [Authorize(Roles = "Administrador")]
    [Tags("Admin / Auditoria")]
    public class AdminAuditoriaController : ControllerBase
    {
        private readonly IAuditoriaRepository _auditoria;
        private readonly IExportadorAuditoria _exportador;
        private readonly IAuditoriaService _auditoriaService;

        public AdminAuditoriaController(
            IAuditoriaRepository auditoria,
            IExportadorAuditoria exportador,
            IAuditoriaService auditoriaService)
        {
            _auditoria = auditoria;
            _exportador = exportador;
            _auditoriaService = auditoriaService;
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
                var resultado = await _auditoria.ConsultarAsync(filtro, cancellationToken);

                // Registrado depois de consultar, com o resultado em mãos: antes,
                // não haveria o que dizer sobre o alcance do acesso, e uma consulta
                // que falhasse deixaria na trilha uma leitura que não aconteceu.
                await _auditoriaService.RegistrarConsultaTrilhaAsync(
                    filtro.Descrever(), resultado.Pagina, resultado.Total, cancellationToken);

                return Ok(resultado);
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
        /// Exporta em XLSX todos os eventos que casam com o filtro.
        /// </summary>
        /// <remarks>
        /// Aceita os mesmos filtros da consulta, mas ignora a paginação de
        /// propósito: quem exporta quer o resultado inteiro, não a página que está
        /// na tela. O teto é FiltroAuditoria.LimiteExportacao — acima dele a
        /// resposta é 400, orientando a estreitar o período, em vez de um arquivo
        /// truncado sem aviso.
        ///
        /// A própria exportação vira um evento na trilha. É o registro mais
        /// importante que este controller produz: é o momento em que os dados
        /// saem do alcance de qualquer controle de acesso.
        /// </remarks>
        [HttpGet("exportar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Exportar(
            [FromQuery] string? tipoEntidade,
            [FromQuery] string? entidadeId,
            [FromQuery] TipoEventoAuditoria? tipoEvento,
            [FromQuery] string? usuarioId,
            [FromQuery] DateTime? dataInicio,
            [FromQuery] DateTime? dataFim,
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
            };

            try
            {
                var eventos = await _auditoria.ConsultarParaExportacaoAsync(filtro, cancellationToken);
                var criterio = filtro.Descrever();

                var planilha = _exportador.GerarPlanilha(eventos, criterio);

                // Registrado DEPOIS de gerar: falhar na geração e mesmo assim
                // gravar "exportou" deixaria na trilha um fato que não aconteceu.
                await _auditoriaService.RegistrarExportacaoAsync(
                    criterio, eventos.Count, cancellationToken);

                var nomeArquivo = $"auditoria-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";

                return File(
                    planilha,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    nomeArquivo);
            }
            catch (ExportacaoExcedeLimiteException ex)
            {
                // 400 e não 500: o servidor está bem, o pedido é que é grande
                // demais. A mensagem traz os números para a pessoa saber o quanto
                // precisa estreitar.
                return BadRequest(new
                {
                    message = $"{ex.Message} Estreite o período ou filtre por entidade.",
                    total = ex.Total,
                    limite = ex.Limite,
                });
            }
            catch (InvalidOperationException)
            {
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
