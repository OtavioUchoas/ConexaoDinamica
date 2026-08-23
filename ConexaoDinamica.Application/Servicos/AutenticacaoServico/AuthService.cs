using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.UsuarioRepositorios;
using ConexaoDinamica.Application.Dtos.UsuariosDtos;
using ConexaoDinamica.Domain.Entidades.Usuarios;
using BCrypt.Net;

namespace ConexaoDinamica.Application.Servicos.AutenticacaoServico
{
    public class UsuarioAuthService : IAuthService
    {
        private readonly ITokenService _tokenService;
        private readonly IUsuarioRepository _usuarioRepository;

        public UsuarioAuthService(ITokenService tokenService, IUsuarioRepository usuarioRepository)
        {
            _tokenService = tokenService;
            _usuarioRepository = usuarioRepository;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var usuario = await _usuarioRepository.ObterPorEmailAsync(request.Email);

            if (usuario == null)
                return null;

            if (!BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
                return null;

            var token = _tokenService.GerarToken(usuario);

            return new LoginResponse
            {
                Token = token,
                Nome = usuario.Nome,
                Email = usuario.Email,
                UsuarioId = usuario.Id
            };
        }

        public async Task<LoginResponse?> CadastroAsync(CadastroUsuario request)
        {
            var usuarioExiste = await _usuarioRepository.UsuarioExisteAsync(request.Email);

            if (usuarioExiste)
                return null;

            var senhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha);

            var usuario = new Usuario
            {
                Nome = request.Nome,
                Email = request.Email,
                SenhaHash = senhaHash,
                DataCriacao = DateTime.UtcNow
            };

            var usuarioCriado = await _usuarioRepository.CriarAsync(usuario);

            var token = _tokenService.GerarToken(usuarioCriado);

            return new LoginResponse
            {
                Token = token,
                Nome = usuarioCriado.Nome,
                Email = usuarioCriado.Email,
                UsuarioId = usuarioCriado.Id
            };
        }

    }
}

