using ConexaoDinamica.Domain.Entidades.Usuarios;

namespace ConexaoDinamica.Application.AplicationInterfaces.Autenticacao
{
    public interface ITokenService
    {
        string GerarToken(Usuario usuario);
    }
}
