using ConexaoDinamica.Application.Dtos.UsuariosDtos;

namespace ConexaoDinamica.Application.AplicationInterfaces.Autenticacao
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<LoginResponse?> CadastroAsync(CadastroUsuario request);
    }
}
