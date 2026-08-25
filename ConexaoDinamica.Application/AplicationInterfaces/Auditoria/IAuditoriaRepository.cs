using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Application.AplicationInterfaces.Auditoria
{
    /// <summary>
    /// Escrita da trilha de auditoria. A implementação vive na Infrastructure,
    /// que conhece o driver do Mongo.
    /// </summary>
    public interface IAuditoriaRepository
    {
        /// <summary>
        /// Grava um lote de eventos.
        ///
        /// Recebe lote e não evento único porque uma única chamada a SaveChanges
        /// costuma alterar várias entidades — gravar de uma vez evita uma ida ao
        /// Mongo por entidade.
        /// </summary>
        Task RegistrarAsync(IReadOnlyList<EventoAuditoria> eventos, CancellationToken cancellationToken = default);
    }
}
