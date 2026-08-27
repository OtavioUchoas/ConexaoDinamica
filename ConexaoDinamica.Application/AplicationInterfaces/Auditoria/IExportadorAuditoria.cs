using ConexaoDinamica.Application.Auditoria;

namespace ConexaoDinamica.Application.AplicationInterfaces.Auditoria
{
    /// <summary>
    /// Transforma eventos da trilha em uma planilha.
    ///
    /// A interface fica aqui e a implementação na Infrastructure pelo mesmo motivo
    /// do repositório do Mongo: gerar XLSX exige uma biblioteca de terceiros, e a
    /// Application não conhece bibliotecas — ela descreve o que precisa acontecer,
    /// não com o quê.
    ///
    /// Devolve byte[] e não Stream porque a planilha é montada inteira em memória
    /// de qualquer forma (o formato XLSX é um zip com índice central, que só pode
    /// ser fechado quando a última célula existe). Fingir streaming aqui daria uma
    /// falsa impressão de consumo constante de memória — o teto real vem de
    /// FiltroAuditoria.LimiteExportacao.
    /// </summary>
    public interface IExportadorAuditoria
    {
        /// <summary>
        /// Monta a planilha com duas abas: uma linha por evento e uma linha por
        /// campo alterado.
        /// </summary>
        /// <param name="eventos">Eventos já filtrados e ordenados.</param>
        /// <param name="criterio">Filtros aplicados, registrados na própria planilha.</param>
        byte[] GerarPlanilha(IReadOnlyList<EventoAuditoria> eventos, string criterio);
    }
}
