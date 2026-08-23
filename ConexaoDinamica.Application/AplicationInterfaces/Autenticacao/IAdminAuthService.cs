using ConexaoDinamica.Application.Dtos.AdminDtos;

namespace ConexaoDinamica.Application.AplicationInterfaces.Autenticacao
{
    /// <summary>
    /// Autenticação do admin de bootstrap. Separada de <see cref="IAuthService"/>
    /// por não depender de repositório/banco — precisa funcionar mesmo sem
    /// conexão configurada.
    /// </summary>
    public interface IAdminAuthService
    {
        Task<AdminLoginResponse?> LoginAsync(AdminLoginRequest request);
    }
}
