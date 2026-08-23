using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.Configuracoes;
using ConexaoDinamica.Application.Dtos.AdminDtos;
using ConexaoDinamica.Domain.Entidades.Usuarios;
using ConexaoDinamica.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ConexaoDinamica.Application.Servicos.AutenticacaoServico
{
    public class AdminAuthService : IAdminAuthService
    {
        private readonly ITokenService _tokenService;
        private readonly AdminBootstrapOptions _options;

        public AdminAuthService(ITokenService tokenService, IOptions<AdminBootstrapOptions> options)
        {
            _tokenService = tokenService;
            _options = options.Value;
        }

        public Task<AdminLoginResponse?> LoginAsync(AdminLoginRequest request)
        {
            if (!_options.EstaConfigurado)
                return Task.FromResult<AdminLoginResponse?>(null);

            if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Senha))
                return Task.FromResult<AdminLoginResponse?>(null);

            var identificadorValido =
                (!string.IsNullOrWhiteSpace(_options.Username) &&
                 string.Equals(request.Login, _options.Username, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(_options.Email) &&
                 string.Equals(request.Login, _options.Email, StringComparison.OrdinalIgnoreCase));

            // Verifica a senha mesmo com identificador inválido: sem isso o tempo de
            // resposta denuncia qual dos dois campos estava errado.
            var senhaValida = BCrypt.Net.BCrypt.Verify(request.Senha, _options.SenhaHash);

            if (!identificadorValido || !senhaValida)
                return Task.FromResult<AdminLoginResponse?>(null);

            // Admin bootstrap não existe no banco: entidade em memória apenas para
            // carregar as claims do token (Perfil define a role de autorização).
            var admin = new Usuario
            {
                Nome = _options.Nome,
                Email = _options.Email,
                Perfil = PerfilUsuario.Administrador
            };

            var response = new AdminLoginResponse
            {
                Token = _tokenService.GerarToken(admin),
                Nome = admin.Nome,
                Email = admin.Email,
                Perfil = admin.Perfil.ToString()
            };

            return Task.FromResult<AdminLoginResponse?>(response);
        }
    }
}
