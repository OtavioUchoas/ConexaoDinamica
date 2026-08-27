using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Application.AplicationInterfaces.Auditoria
{
    /// <summary>
    /// Leitura e escrita da trilha de auditoria. A implementação vive na
    /// Infrastructure, que conhece o driver do Mongo.
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

        /// <summary>
        /// Consulta a trilha, do mais recente para o mais antigo.
        ///
        /// Diferente da escrita, a leitura PROPAGA falhas: quem consulta precisa
        /// saber que o resultado não veio. Engolir o erro aqui mostraria uma lista
        /// vazia, indistinguível de "nenhum evento encontrado" — e uma trilha de
        /// auditoria que parece vazia por engano é pior que um erro visível.
        /// </summary>
        Task<ResultadoPaginado<EventoAuditoria>> ConsultarAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Todos os eventos que casam com o filtro, sem paginação, para exportação.
        ///
        /// Existe separado de ConsultarAsync porque a intenção é outra: aquela
        /// devolve uma página para a tela, esta devolve o conjunto inteiro para
        /// virar arquivo. Fossem o mesmo método, a tela poderia pedir tudo por
        /// engano — e é justamente para impedir isso que TamanhoMaximoPagina
        /// existe.
        ///
        /// Lança <see cref="ExportacaoExcedeLimiteException"/> se o resultado
        /// passar de FiltroAuditoria.LimiteExportacao: melhor recusar e mandar
        /// estreitar o filtro do que entregar uma planilha silenciosamente
        /// incompleta, que numa auditoria seria pior que nenhuma.
        /// </summary>
        Task<IReadOnlyList<EventoAuditoria>> ConsultarParaExportacaoAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tipos de entidade presentes na trilha, para alimentar o filtro da
        /// interface sem precisar de uma lista fixa no código.
        /// </summary>
        Task<IReadOnlyList<string>> ObterTiposEntidadeAsync(CancellationToken cancellationToken = default);
    }
}
