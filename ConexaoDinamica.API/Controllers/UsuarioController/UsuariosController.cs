using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.UsuarioRepositorios;
using ConexaoDinamica.Application.Dtos.UsuariosDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConexaoDinamica.API.Controllers.UsuarioController
{
    /// <summary>
    /// Consulta de usuários.
    ///
    /// Existe principalmente para exercitar a auditoria de visualização: é o caso
    /// de uso clássico — abrir o detalhe de um registro individual, que é
    /// justamente o momento em que o acesso merece ser registrado.
    /// </summary>
    [ApiController]
    [Route("api/v1/usuarios")]
    [Authorize]
    [Tags("Usuários")]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IAuditoriaService _auditoriaService;

        public UsuariosController(
            IUsuarioRepository usuarioRepository,
            IAuditoriaService auditoriaService)
        {
            _usuarioRepository = usuarioRepository;
            _auditoriaService = auditoriaService;
        }

        /// <summary>
        /// Detalhe de um usuário. Registra evento de visualização.
        /// </summary>
        /// <remarks>
        /// A auditoria é registrada apenas quando o registro é EFETIVAMENTE
        /// entregue. Auditar antes da busca marcaria como "visualizado" um id
        /// inexistente, sujando a trilha com acessos que nunca aconteceram — e
        /// dando a um curioso a chance de poluir a auditoria só chutando ids.
        /// </remarks>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(UsuarioDetalheResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObterPorId(int id, CancellationToken cancellationToken)
        {
            var usuario = await _usuarioRepository.ObterPorIdAsync(id);

            if (usuario is null)
                return NotFound(new { message = "Usuário não encontrado." });

            await _auditoriaService.RegistrarVisualizacaoAsync(
                nameof(ConexaoDinamica.Domain.Entidades.Usuarios.Usuario),
                usuario.Id.ToString(),
                cancellationToken);

            return Ok(new UsuarioDetalheResponse
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Perfil = usuario.Perfil.ToString(),
                DataCriacao = usuario.DataCriacao
            });
        }
    }
}
