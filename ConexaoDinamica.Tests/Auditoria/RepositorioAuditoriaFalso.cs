using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Tests.Auditoria
{
    /// <summary>
    /// Guarda em memória o que teria ido para o Mongo.
    ///
    /// Os testes verificam o EVENTO produzido, não a gravação: o que é sutil no
    /// interceptor é montar o documento certo, e um Mongo de verdade só
    /// acrescentaria lentidão e uma dependência externa a essa verificação.
    ///
    /// <see cref="FalharAoRegistrar"/> existe para o caso oposto — provar que uma
    /// falha na publicação não chega ao chamador do SaveChanges.
    /// </summary>
    internal sealed class RepositorioAuditoriaFalso : IAuditoriaRepository
    {
        public List<EventoAuditoria> Eventos { get; } = [];

        /// <summary>Quando true, RegistrarAsync lança em vez de guardar.</summary>
        public bool FalharAoRegistrar { get; set; }

        public EventoAuditoria Unico => Assert.Single(Eventos);

        public Task RegistrarAsync(
            IReadOnlyList<EventoAuditoria> eventos,
            CancellationToken cancellationToken = default)
        {
            if (FalharAoRegistrar)
                throw new InvalidOperationException("Mongo indisponível (simulado).");

            Eventos.AddRange(eventos);
            return Task.CompletedTask;
        }

        // Os testes deste arquivo exercitam a escrita. A leitura tem implementação
        // própria no MongoAuditoriaRepository e não passa pelo interceptor.

        public Task<ResultadoPaginado<EventoAuditoria>> ConsultarAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EventoAuditoria>> ConsultarParaExportacaoAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ObterTiposEntidadeAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
