using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.Dtos.AdminDtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ConexaoDinamica.API.Controllers.AdminController
{
    [ApiController]
    [Route("api/v1/admin")]
    [Tags("Admin / Login")]
    public class AdminControllers : ControllerBase
    {
        private readonly IAdminAuthService _adminAuthService;

        public AdminControllers(IAdminAuthService adminAuthService)
        {
            _adminAuthService = adminAuthService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> LoginAdmin(AdminLoginRequest loginRequest)
        {
            var result = await _adminAuthService.LoginAsync(loginRequest);

            if (result == null)
            {
                return Unauthorized(new { message = "Credenciais inválidas" });
            }

            return Ok(result);
        }
    }
}
