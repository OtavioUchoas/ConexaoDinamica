using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Registro explícito de eventos que o interceptor não captura.
    ///
    /// Hoje cobre apenas visualização. Eventos de negócio que não se traduzem em
    /// alteração de dados ("exportou relatório", "reenviou notificação") entram
    /// por aqui também quando surgirem.
    /// </summary>
    public class AuditoriaService : IAuditoriaService
    {
        private readonly IAuditoriaRepository _repository;
        private readonly IContextoAuditoria _contexto;

        public AuditoriaService(IAuditoriaRepository repository, IContextoAuditoria contexto)
        {
            _repository = repository;
            _contexto = contexto;
        }

        public Task RegistrarVisualizacaoAsync(
            string tipoEntidade,
            string entidadeId,
            CancellationToken cancellationToken = default)
        {
            var evento = new EventoAuditoria
            {
                TipoEvento = TipoEventoAuditoria.Visualizacao,
                CorrelationId = _contexto.ObterCorrelationId(),
                Usuario = _contexto.ObterUsuario(),
                Origem = _contexto.ObterOrigem(),
                Entidade = new EntidadeAuditada
                {
                    Tipo = tipoEntidade,
                    Id = entidadeId
                }
                // Sem Alteracoes e sem Snapshot de propósito: uma consulta não
                // muda nada, e copiar o registro inteiro a cada leitura inflaria
                // a trilha sem acrescentar informação — o estado daquele momento
                // já está no último evento de dados da mesma entidade.
            };

            return _repository.RegistrarAsync([evento], cancellationToken);
        }

        public Task RegistrarExportacaoAsync(
            string criterio,
            int quantidade,
            CancellationToken cancellationToken = default)
        {
            var evento = new EventoAuditoria
            {
                TipoEvento = TipoEventoAuditoria.Exportacao,
                CorrelationId = _contexto.ObterCorrelationId(),
                Usuario = _contexto.ObterUsuario(),
                Origem = _contexto.ObterOrigem(),
                Entidade = new EntidadeAuditada
                {
                    // A "entidade" aqui é a própria trilha, não um registro dela.
                    // Sem um tipo próprio, o evento apareceria na consulta como se
                    // pertencesse a algum Pedido ou Cliente específico.
                    Tipo = "TrilhaAuditoria",
                    Id = "*"
                },
                // O critério vai no Snapshot, e não em Alteracoes: nada mudou, o
                // que existe é o retrato do que foi levado.
                Snapshot = new Dictionary<string, object?>
                {
                    ["Criterio"] = criterio,
                    ["QuantidadeEventos"] = quantidade,
                    ["Formato"] = "XLSX"
                }
            };

            return _repository.RegistrarAsync([evento], cancellationToken);
        }
    }
}
