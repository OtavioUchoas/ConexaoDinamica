using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Grava a trilha de auditoria no MongoDB.
    ///
    /// ── Política de falha: engolir e registrar ────────────────────────────────
    /// Se o Mongo estiver indisponível, a operação de negócio NÃO é interrompida:
    /// a falha vai para o log da aplicação e o fluxo segue.
    ///
    /// Isto é uma decisão consciente, não um catch esquecido. A consequência é
    /// real e precisa ser dita: numa queda do Mongo, eventos são perdidos em
    /// definitivo e a trilha fica com buracos silenciosos.
    ///
    /// A alternativa correta, quando perder evento for inaceitável, é o padrão
    /// Outbox: gravar o evento no Postgres dentro da MESMA transação da alteração
    /// e publicar no Mongo por um worker em segundo plano. Como é a mesma
    /// transação, ou os dois acontecem ou nenhum — o que elimina a janela do
    /// dual-write que existe aqui.
    /// </summary>
    public class MongoAuditoriaRepository : IAuditoriaRepository
    {
        private const string NomeColecao = "eventos_auditoria";

        private readonly IMongoConexaoProvider _provider;
        private readonly ILogger<MongoAuditoriaRepository> _logger;

        public MongoAuditoriaRepository(
            IMongoConexaoProvider provider,
            ILogger<MongoAuditoriaRepository> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task RegistrarAsync(
            IReadOnlyList<EventoAuditoria> eventos,
            CancellationToken cancellationToken = default)
        {
            if (eventos.Count == 0)
                return;

            try
            {
                var database = _provider.ObterDatabase();

                if (database is null)
                {
                    // Não deveria acontecer: o modo setup impede a aplicação de
                    // operar sem o Mongo configurado. Registrado como aviso porque,
                    // se ocorrer, indica falha naquele guard.
                    _logger.LogWarning(
                        "Auditoria ignorada: MongoDB não configurado. {Quantidade} evento(s) perdido(s).",
                        eventos.Count);
                    return;
                }

                var colecao = database.GetCollection<EventoAuditoria>(NomeColecao);
                await colecao.InsertManyAsync(eventos, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Nível Error, e não Warning: perder trilha de auditoria é um
                // problema que alguém precisa investigar, mesmo que a operação de
                // negócio tenha seguido normalmente.
                _logger.LogError(ex,
                    "Falha ao gravar auditoria no MongoDB. {Quantidade} evento(s) perdido(s). " +
                    "Entidades: {Entidades}",
                    eventos.Count,
                    string.Join(", ", eventos.Select(e => $"{e.Entidade.Tipo}#{e.Entidade.Id}")));
            }
        }
    }
}
