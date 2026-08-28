using ConexaoDinamica.Application.Auditoria;
using ConexaoDinamica.Infrastructure.Auditoria;

namespace ConexaoDinamica.Tests.Auditoria
{
    /// <summary>
    /// Identidade fixa no lugar do HttpContext.
    ///
    /// <see cref="FalharAoObterUsuario"/> simula a coleta quebrando na primeira
    /// linha da fase 1, para provar que uma falha ali não impede a gravação do
    /// dado de negócio.
    /// </summary>
    internal sealed class ContextoAuditoriaFalso : IContextoAuditoria
    {
        public bool FalharAoObterUsuario { get; set; }

        public UsuarioAuditado? Usuario { get; set; } = new()
        {
            Id = "7",
            Nome = "Fulano de Teste",
            Email = "fulano@teste.local"
        };

        public UsuarioAuditado? ObterUsuario() =>
            FalharAoObterUsuario
                ? throw new InvalidOperationException("Contexto indisponível (simulado).")
                : Usuario;

        public OrigemAuditada? ObterOrigem() =>
            new() { Ip = "203.0.113.7", UserAgent = "xunit" };

        public string? ObterCorrelationId() => "correlacao-de-teste";
    }
}
