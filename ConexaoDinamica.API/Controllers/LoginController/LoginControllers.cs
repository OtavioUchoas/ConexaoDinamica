using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ConexaoDinamica.Application.Dtos.UsuariosDtos;
using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.API.Controllers.LoginController
{
    /// <summary>
    /// Entrada de usuários no sistema.
    ///
    /// ── Por que o log não bastava ─────────────────────────────────────────────
    /// Sucesso e falha de login já eram registrados via ILogger, e isso continua
    /// valendo para diagnóstico. Mas o log da aplicação é volátil, rotaciona por
    /// tamanho e vive fora da trilha: não dá para cruzar "quem alterou este
    /// pedido" com "de onde essa pessoa entrou", nem responder "quantas
    /// tentativas falharam nesta conta na semana passada" — que é exatamente o
    /// tipo de pergunta que a auditoria existe para responder.
    ///
    /// O cadastro não registra evento próprio: ele grava um Usuario no Postgres e
    /// o interceptor já o captura como Adicao.
    /// </summary>
    [ApiController]
    [Route("api/v1")]
    [Tags("Autenticação / Login")]
    public class LoginControllers : ControllerBase
    {
        /// <summary>
        /// Origem da credencial, gravada no evento. Distingue esta porta de
        /// entrada da do admin de bootstrap, que tem outros poderes.
        /// </summary>
        private const string Credencial = "Usuario";

        private readonly IAuthService _authService;
        private readonly IAuditoriaService _auditoria;
        private readonly ILogger<LoginControllers> _logger;

        public LoginControllers(
            IAuthService authService,
            IAuditoriaService auditoria,
            ILogger<LoginControllers> logger)
        {
            _authService = authService;
            _auditoria = auditoria;
            _logger = logger;
        }

        /// <summary>
        /// Realiza login de um usuário
        /// </summary>
        /// <param name="request">Email e senha do usuário</param>
        /// <returns>Token JWT e dados do usuário</returns>
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Tentativa de login para email: {Email}", request.Email);

                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    _logger.LogWarning("Falha no login - credenciais inválidas para email: {Email}", request.Email);

                    // O motivo é o mesmo que a resposta dá, e não mais que isso:
                    // separar "conta inexistente" de "senha errada" na trilha
                    // devolveria a quem a lê a enumeração de contas que o 401
                    // genérico evita.
                    await _auditoria.RegistrarFalhaAutenticacaoAsync(
                        Credencial, request.Email, "Credenciais inválidas", cancellationToken);

                    return Unauthorized(new
                    {
                        message = "Email ou senha inválidos"
                    });
                }

                _logger.LogInformation("Login bem-sucedido para usuário: {UsuarioId}", result.UsuarioId);

                await _auditoria.RegistrarAutenticacaoAsync(
                    Credencial,
                    request.Email,
                    new UsuarioAuditado
                    {
                        Id = result.UsuarioId.ToString(),
                        Nome = result.Nome,
                        Email = result.Email
                    },
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Sem evento de auditoria aqui: um 500 não é tentativa recusada, é
                // pergunta não respondida. Registrá-lo como falha de autenticação
                // contaminaria a contagem que denuncia ataque de força bruta.
                _logger.LogError(ex, "Erro ao processar login");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Erro ao processar login"
                });
            }
        }

        /// <summary>
        /// Realiza cadastro de um novo usuário
        /// </summary>
        /// <param name="request">Dados do novo usuário</param>
        /// <returns>Token JWT e dados do usuário criado</returns>
        [HttpPost("cadastro")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Cadastro([FromBody] CadastroUsuario request)
        {
            try
            {
                _logger.LogInformation("Tentativa de cadastro para email: {Email}", request.Email);

                var result = await _authService.CadastroAsync(request);

                if (result == null)
                {
                    _logger.LogWarning("Falha no cadastro - email já existe: {Email}", request.Email);
                    return BadRequest(new 
                    { 
                        message = "Email já cadastrado"
                    });
                }

                _logger.LogInformation("Cadastro bem-sucedido - novo usuário: {UsuarioId}", result.UsuarioId);
                return Created(nameof(Cadastro), result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar cadastro");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Erro ao processar cadastro"
                });
            }
        }
    }
}


