using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.Dtos.AdminDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoDinamica.API.Controllers.AdminController
{
    /// <summary>
    /// Endpoints do AdminCenter para configurar a conexão do Postgres.
    ///
    /// ── Sobre a autorização ───────────────────────────────────────────────────
    /// [Authorize(Roles = "Administrador")] no nível da classe protege TODAS as
    /// actions. Isso importa mais aqui do que em qualquer outro controller: quem
    /// alcança estes endpoints aponta a aplicação para o banco que quiser.
    ///
    /// A role vem da claim emitida em TokenService.GerarToken a partir de
    /// Usuario.Perfil. O admin de bootstrap recebe PerfilUsuario.Administrador,
    /// e é por isso que ele consegue configurar o sistema sem que exista um único
    /// usuário no banco — que é justamente o ponto dele existir.
    ///
    /// O login (AdminControllers) fica de fora dessa proteção, por motivo óbvio:
    /// é onde o token é obtido.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/conexao")]
    [Authorize(Roles = "Administrador")]
    [Tags("Admin / Conexão")]
    public class AdminConexaoController : ControllerBase
    {
        private readonly IConexaoAdminService _conexaoAdminService;
        private readonly ILogger<AdminConexaoController> _logger;

        public AdminConexaoController(
            IConexaoAdminService conexaoAdminService,
            ILogger<AdminConexaoController> logger)
        {
            _conexaoAdminService = conexaoAdminService;
            _logger = logger;
        }

        /// <summary>
        /// Configuração atual do Postgres (nunca inclui a senha).
        /// </summary>
        /// <response code="200">Configuração encontrada</response>
        /// <response code="404">Nunca foi configurado</response>
        [HttpGet("postgres")]
        [ProducesResponseType(typeof(ConexaoPostgresResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult ObterPostgres()
        {
            var config = _conexaoAdminService.ObterConfiguracao();

            if (config is null)
                return NotFound(new { message = "Nenhuma conexão configurada." });

            return Ok(config);
        }

        /// <summary>
        /// Testa os dados informados sem salvar nada.
        /// </summary>
        /// <remarks>
        /// Responde 200 mesmo quando a conexão falha — o resultado vai em
        /// Sucesso/Mensagem. O endpoint cumpriu seu papel: a pergunta era "dá para
        /// conectar?" e "não, senha inválida" é uma resposta legítima, não um erro
        /// do servidor.
        /// </remarks>
        [HttpPost("postgres/testar")]
        [ProducesResponseType(typeof(TesteConexaoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TestarPostgres(
            [FromBody] ConexaoPostgresRequest request,
            CancellationToken cancellationToken)
        {
            var resultado = await _conexaoAdminService.TestarAsync(request, cancellationToken);

            // Host e banco entram no log; usuário e senha não. Um log de auditoria
            // não deve virar uma fonte alternativa de credenciais.
            _logger.LogInformation(
                "Teste de conexão para {Host}:{Porta}/{Database} -> {Resultado} ({TempoMs}ms)",
                request.Host, request.Porta, request.Database,
                resultado.Sucesso ? "sucesso" : "falha", resultado.TempoMs);

            return Ok(resultado);
        }

        /// <summary>
        /// Salva a configuração. Passa a valer na próxima requisição, sem reiniciar.
        /// </summary>
        [HttpPut("postgres")]
        [ProducesResponseType(typeof(ConexaoPostgresResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult SalvarPostgres([FromBody] ConexaoPostgresRequest request)
        {
            var salva = _conexaoAdminService.Salvar(request);

            _logger.LogWarning(
                "Conexão do Postgres alterada para {Host}:{Porta}/{Database}",
                salva.Host, salva.Porta, salva.Database);

            return Ok(salva);
        }

        /// <summary>
        /// Migrations aplicadas e pendentes no banco configurado.
        /// </summary>
        [HttpGet("postgres/migrations")]
        [ProducesResponseType(typeof(StatusMigrationsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> StatusMigrations(CancellationToken cancellationToken)
        {
            var status = await _conexaoAdminService.ObterStatusMigrationsAsync(cancellationToken);
            return Ok(status);
        }

        /// <summary>
        /// Aplica as migrations pendentes (cria o banco se não existir).
        /// </summary>
        /// <remarks>
        /// É o substituto do Migrate() que rodava no startup. Migrar deixou de ser
        /// efeito colateral da inicialização e virou uma ação deliberada do
        /// administrador — condição para a aplicação subir sem banco configurado.
        /// </remarks>
        [HttpPost("postgres/migrations")]
        [ProducesResponseType(typeof(AplicarMigrationsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(AplicarMigrationsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AplicarMigrations(CancellationToken cancellationToken)
        {
            var resultado = await _conexaoAdminService.AplicarMigrationsAsync(cancellationToken);

            _logger.LogWarning("Aplicação de migrations solicitada -> {Mensagem}", resultado.Mensagem);

            // Aqui, diferente do teste de conexão, a falha É um erro: o
            // administrador mandou executar uma ação e ela não aconteceu.
            return resultado.Sucesso ? Ok(resultado) : BadRequest(resultado);
        }

        // ── MongoDB (logs de auditoria) ────────────────────────────────────────

        /// <summary>
        /// Configuração atual do MongoDB (nunca inclui a senha).
        /// </summary>
        /// <response code="200">Configuração encontrada</response>
        /// <response code="404">Nunca foi configurado</response>
        [HttpGet("mongo")]
        [ProducesResponseType(typeof(ConexaoMongoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult ObterMongo()
        {
            var config = _conexaoAdminService.ObterConfiguracaoMongo();

            if (config is null)
                return NotFound(new { message = "Nenhuma conexão do MongoDB configurada." });

            return Ok(config);
        }

        /// <summary>
        /// Testa os dados informados sem salvar nada.
        /// </summary>
        /// <remarks>
        /// Como no Postgres, responde 200 mesmo quando a conexão falha — o
        /// resultado vai em Sucesso/Mensagem.
        /// </remarks>
        [HttpPost("mongo/testar")]
        [ProducesResponseType(typeof(TesteConexaoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TestarMongo(
            [FromBody] ConexaoMongoRequest request,
            CancellationToken cancellationToken)
        {
            var resultado = await _conexaoAdminService.TestarMongoAsync(request, cancellationToken);

            _logger.LogInformation(
                "Teste de conexão Mongo para {Host}:{Porta}/{Database} -> {Resultado} ({TempoMs}ms)",
                request.Host, request.Porta, request.Database,
                resultado.Sucesso ? "sucesso" : "falha", resultado.TempoMs);

            return Ok(resultado);
        }

        /// <summary>
        /// Salva a configuração do MongoDB.
        /// </summary>
        [HttpPut("mongo")]
        [ProducesResponseType(typeof(ConexaoMongoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult SalvarMongo([FromBody] ConexaoMongoRequest request)
        {
            var salva = _conexaoAdminService.SalvarMongo(request);

            _logger.LogWarning(
                "Conexão do MongoDB alterada para {Host}:{Porta}/{Database}",
                salva.Host, salva.Porta, salva.Database);

            return Ok(salva);
        }
    }
}
