using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Fonte comum de "quem fez, de onde" para os dois caminhos de auditoria.
    /// </summary>
    public interface IContextoAuditoria
    {
        UsuarioAuditado? ObterUsuario();
        OrigemAuditada? ObterOrigem();
        string? ObterCorrelationId();
    }
}
