using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Registro explícito de eventos que o interceptor não captura.
    ///
    /// São os fatos que não passam por SaveChanges: leitura, saída de dados e
    /// entrada no sistema. Eventos de negócio sem alteração de dados ("reenviou
    /// notificação") entram por aqui também quando surgirem.
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

        public Task RegistrarAutenticacaoAsync(
            string credencial,
            string identificador,
            UsuarioAuditado usuario,
            CancellationToken cancellationToken = default)
        {
            var evento = new EventoAuditoria
            {
                TipoEvento = TipoEventoAuditoria.Autenticacao,
                CorrelationId = _contexto.ObterCorrelationId(),

                // Vem do parâmetro, não do contexto: o token acabou de ser emitido
                // e o HttpContext desta requisição ainda é anônimo.
                Usuario = usuario,
                Origem = _contexto.ObterOrigem(),
                Entidade = MontarEntidadeAutenticacao(identificador),
                Snapshot = new Dictionary<string, object?>
                {
                    ["Credencial"] = credencial
                }
            };

            return _repository.RegistrarAsync([evento], cancellationToken);
        }

        public Task RegistrarFalhaAutenticacaoAsync(
            string credencial,
            string identificador,
            string motivo,
            CancellationToken cancellationToken = default)
        {
            var evento = new EventoAuditoria
            {
                TipoEvento = TipoEventoAuditoria.FalhaAutenticacao,
                CorrelationId = _contexto.ObterCorrelationId(),

                // Sem usuário de propósito: ninguém autenticou. A identificação
                // possível é a origem, abaixo, com o identificador tentado.
                Usuario = null,
                Origem = _contexto.ObterOrigem(),
                Entidade = MontarEntidadeAutenticacao(identificador),
                Snapshot = new Dictionary<string, object?>
                {
                    ["Credencial"] = credencial,
                    ["Motivo"] = motivo
                }
            };

            return _repository.RegistrarAsync([evento], cancellationToken);
        }

        /// <summary>
        /// A "entidade" de um evento de autenticação é a tentativa, não o usuário.
        ///
        /// Poderia ser o Usuario que entrou, mas isso misturaria na trilha dele
        /// dois tipos de identificador: o id numérico dos eventos de dados e o
        /// e-mail das tentativas recusadas, que sequer têm usuário. Com tipo
        /// próprio, "toda a atividade de login" e "todo o histórico do usuário 42"
        /// continuam sendo duas consultas distintas — e ambas simples.
        /// </summary>
        private static EntidadeAuditada MontarEntidadeAutenticacao(string identificador) =>
            new()
            {
                Tipo = "Autenticacao",
                Id = identificador
            };
    }
}
