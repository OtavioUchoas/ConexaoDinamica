using System.Security.Claims;
using ConexaoDinamica.Application.Auditoria;
using Microsoft.AspNetCore.Http;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Extrai quem fez, de onde e sob qual correlação, a partir da requisição atual.
    ///
    /// Existe para não duplicar essa leitura entre o interceptor (eventos de
    /// dados) e o serviço explícito (visualização): os dois precisam exatamente
    /// das mesmas informações, e mantê-las em um só lugar garante que a trilha
    /// fique consistente entre os dois caminhos.
    ///
    /// Fora de uma requisição HTTP — em um job em segundo plano, por exemplo —
    /// todos os métodos retornam null. Um evento sem usuário identificado é
    /// preferível a nenhum evento.
    /// </summary>
    public class ContextoAuditoria : IContextoAuditoria
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ContextoAuditoria(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Dados do usuário autenticado, desnormalizados no momento do evento.
        ///
        /// Guardar apenas o id economizaria espaço, mas a trilha perderia sentido:
        /// se o usuário for renomeado ou removido, "usuarioId: 3" deixa de
        /// identificar quem realmente executou a ação.
        /// </summary>
        public UsuarioAuditado? ObterUsuario()
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return null;

            return new UsuarioAuditado
            {
                Id = user.FindFirst("UserId")?.Value ?? string.Empty,
                Nome = user.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                Email = user.FindFirst(ClaimTypes.Email)?.Value
            };
        }

        public OrigemAuditada? ObterOrigem()
        {
            var context = _httpContextAccessor.HttpContext;

            if (context is null)
                return null;

            return new OrigemAuditada
            {
                Ip = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString()
            };
        }

        /// <summary>
        /// Agrupa todos os eventos originados da mesma requisição. Uma operação de
        /// negócio costuma alterar várias entidades; sem isto a trilha vira eventos
        /// soltos, sem como reconstruir o que aconteceu junto.
        /// </summary>
        public string? ObterCorrelationId() =>
            _httpContextAccessor.HttpContext?.TraceIdentifier;
    }
}
