namespace ConexaoDinamica.Application.Auditoria
{
    public enum TipoEventoAuditoria
    {
        Adicao = 1,
        Alteracao = 2,
        Remocao = 3,
        Visualizacao = 4,

        /// <summary>
        /// Saída de dados da trilha para fora do sistema.
        ///
        /// Não é uma visualização a mais: quem exporta leva consigo o histórico
        /// completo dos registros que casaram com o filtro, e esse arquivo passa a
        /// existir fora de qualquer controle de acesso. O evento registra o
        /// CRITÉRIO e o volume, não os registros — um evento por linha exportada
        /// inflaria a trilha sem acrescentar nada.
        /// </summary>
        Exportacao = 5
    }
}
