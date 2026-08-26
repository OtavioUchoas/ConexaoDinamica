namespace ConexaoDinamica.Application.Auditoria
{
    /// <summary>
    /// Página de resultados com o total geral.
    ///
    /// O total acompanha a página porque a interface precisa dele para montar a
    /// paginação — sem ele, não há como saber se existe página seguinte sem
    /// tentar buscá-la.
    /// </summary>
    public class ResultadoPaginado<T>
    {
        public IReadOnlyList<T> Itens { get; set; } = [];

        /// <summary>Total de registros que atendem ao filtro, ignorando a página.</summary>
        public long Total { get; set; }

        public int Pagina { get; set; }

        public int TamanhoPagina { get; set; }

        public int TotalPaginas =>
            TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
    }
}
