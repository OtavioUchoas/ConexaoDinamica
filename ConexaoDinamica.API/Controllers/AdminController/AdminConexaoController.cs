using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
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
    ///
    /// ── Sobre a auditoria ─────────────────────────────────────────────────────
    /// Só as ações que MUDAM alguma coisa geram evento: salvar conexão e aplicar
    /// migrations. Consultar a configuração e testar uma conexão ficam no log —
    /// não alteram nada e, sendo o painel do administrador, cada abertura de tela
    /// dispararia um evento sem fato correspondente.
    ///
    /// Nenhum evento leva senha. O log desta classe já seguia essa regra; a
    /// trilha, que é mais duradoura e costuma ter acesso mais amplo, segue com
    /// mais razão ainda.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/conexao")]
    [Authorize(Roles = "Administrador")]
    [Tags("Admin / Conexão")]
    public class AdminConexaoController : ControllerBase
    {
        private readonly IConexaoAdminService _conexaoAdminService;
        private readonly IAuditoriaService _auditoria;
        private readonly ILogger<AdminConexaoController> _logger;

        public AdminConexaoController(
            IConexaoAdminService conexaoAdminService,
            IAuditoriaService auditoria,
            ILogger<AdminConexaoController> logger)
        {
            _conexaoAdminService = conexaoAdminService;
            _auditoria = auditoria;
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
        public async Task<IActionResult> SalvarPostgres(
            [FromBody] ConexaoPostgresRequest request,
            CancellationToken cancellationToken)
        {
            // Lido ANTES de salvar: depois de gravar, a configuração anterior não
            // existe mais em lugar nenhum, e "para onde apontava antes" é metade
            // do que se quer saber ao investigar uma troca de banco.
            var anterior = _conexaoAdminService.ObterConfiguracao();

            var salva = _conexaoAdminService.Salvar(request);

            _logger.LogWarning(
                "Conexão do Postgres alterada para {Host}:{Porta}/{Database}",
                salva.Host, salva.Porta, salva.Database);

            await _auditoria.RegistrarConfiguracaoAsync(
                "ConexaoPostgres",
                new Dictionary<string, object?>
                {
                    ["Host"] = salva.Host,
                    ["Porta"] = salva.Porta,
                    ["Database"] = salva.Database,
                    ["Usuario"] = salva.Usuario,
                    ["SenhaDefinida"] = salva.SenhaDefinida,
                    ["Anterior"] = Descrever(anterior?.Host, anterior?.Porta, anterior?.Database),
                },
                cancellationToken);

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

            // A tentativa é registrada mesmo quando falha, ao contrário do que se
            // faz na exportação. Lá o evento afirma que dados saíram, e afirmá-lo
            // sem que tenham saído seria falso; aqui o fato auditável é a ordem
            // dada — alguém mandou alterar o esquema do banco, e o resultado é
            // parte do registro, não condição para ele existir.
            await _auditoria.RegistrarConfiguracaoAsync(
                "Migrations",
                new Dictionary<string, object?>
                {
                    ["Sucesso"] = resultado.Sucesso,
                    ["Mensagem"] = resultado.Mensagem,
                    ["Aplicadas"] = string.Join(", ", resultado.Aplicadas),

                    // Só o fato de ter sido criado. A senha provisória aparece uma
                    // única vez na resposta e não pode ficar guardada na trilha.
                    ["SuperUsuarioCriado"] = resultado.SuperUsuario is not null,
                },
                cancellationToken);

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
        public async Task<IActionResult> SalvarMongo(
            [FromBody] ConexaoMongoRequest request,
            CancellationToken cancellationToken)
        {
            var anterior = _conexaoAdminService.ObterConfiguracaoMongo();

            var salva = _conexaoAdminService.SalvarMongo(request);

            _logger.LogWarning(
                "Conexão do MongoDB alterada para {Host}:{Porta}/{Database}",
                salva.Host, salva.Porta, salva.Database);

            // Este evento é diferente de todos os outros: ele muda o lugar onde a
            // própria trilha é gravada, e por ser registrado DEPOIS da troca, cai
            // no destino novo. É intencional — a trilha nova nasce dizendo de onde
            // veio e quem a trouxe.
            //
            // A contrapartida é honesta: a trilha antiga termina sem explicação.
            // Fechá-la exigiria gravar nos dois bancos, e o campo "Anterior" aqui
            // dá o caminho de volta para quem precisar procurar lá.
            await _auditoria.RegistrarConfiguracaoAsync(
                "ConexaoMongo",
                new Dictionary<string, object?>
                {
                    ["Host"] = salva.Host,
                    ["Porta"] = salva.Porta,
                    ["Database"] = salva.Database,
                    ["Usuario"] = salva.Usuario,
                    ["AuthSource"] = salva.AuthSource,
                    ["SenhaDefinida"] = salva.SenhaDefinida,
                    ["Anterior"] = Descrever(anterior?.Host, anterior?.Porta, anterior?.Database),
                },
                cancellationToken);

            return Ok(salva);
        }

        /// <summary>
        /// Resume um destino em uma linha ("localhost:5432/conexao"), para o campo
        /// "Anterior" dos eventos de configuração. Devolve "não configurado" quando
        /// não havia nada antes — a primeira configuração também é um fato, e
        /// deixar o campo vazio a tornaria indistinguível de uma falha de captura.
        /// </summary>
        private static string Descrever(string? host, int? porta, string? database) =>
            string.IsNullOrWhiteSpace(host)
                ? "não configurado"
                : $"{host}:{porta}/{database}";
    }
}
