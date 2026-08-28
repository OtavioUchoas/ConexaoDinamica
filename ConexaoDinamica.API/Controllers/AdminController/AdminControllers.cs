using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Application.Dtos.AdminDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConexaoDinamica.API.Controllers.AdminController
{
    /// <summary>
    /// Entrada do administrador de bootstrap.
    ///
    /// É a porta com mais poder do sistema: quem passa por aqui aponta a aplicação
    /// para o banco que quiser e lê a trilha inteira. Auditar esta autenticação
    /// importa mais do que auditar a dos usuários comuns — e é a credencial que
    /// não tem dono no banco, então o log era, até aqui, o único vestígio de que
    /// alguém a usou.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin")]
    [Tags("Admin / Login")]
    public class AdminControllers : ControllerBase
    {
        /// <summary>
        /// Origem da credencial, gravada no evento. Separa esta porta da de
        /// usuários comuns na hora de consultar a trilha.
        /// </summary>
        private const string Credencial = "AdminBootstrap";

        private readonly IAdminAuthService _adminAuthService;
        private readonly IAuditoriaService _auditoria;

        public AdminControllers(
            IAdminAuthService adminAuthService,
            IAuditoriaService auditoria)
        {
            _adminAuthService = adminAuthService;
            _auditoria = auditoria;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> LoginAdmin(
            AdminLoginRequest loginRequest,
            CancellationToken cancellationToken)
        {
            var result = await _adminAuthService.LoginAsync(loginRequest);

            if (result == null)
            {
                await _auditoria.RegistrarFalhaAutenticacaoAsync(
                    Credencial, loginRequest.Login, "Credenciais inválidas", cancellationToken);

                return Unauthorized(new { message = "Credenciais inválidas" });
            }

            // O admin de bootstrap não existe no banco: não há id para gravar, e o
            // identificador informado é o que o identifica. Deixar o Id vazio seria
            // pior — na trilha ele apareceria como um usuário sem identidade.
            await _auditoria.RegistrarAutenticacaoAsync(
                Credencial,
                loginRequest.Login,
                new UsuarioAuditado
                {
                    Id = Credencial,
                    Nome = result.Nome,
                    Email = result.Email
                },
                cancellationToken);

            return Ok(result);
        }
    }
}
