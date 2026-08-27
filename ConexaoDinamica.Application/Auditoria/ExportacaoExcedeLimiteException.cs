namespace ConexaoDinamica.Application.Auditoria
{
    /// <summary>
    /// A consulta pedida para exportação devolve mais eventos do que o limite.
    ///
    /// ── Por que uma exceção própria ──────────────────────────────────────────
    /// Deliberadamente NÃO herda de InvalidOperationException: o repositório já
    /// usa aquele tipo para sinalizar "MongoDB não configurado", que o controller
    /// traduz em 503. Herdando dele, uma exportação grande demais seria reportada
    /// como indisponibilidade do banco — erro do servidor, quando na verdade é o
    /// pedido que precisa ser estreitado.
    ///
    /// ── Por que recusar em vez de truncar ────────────────────────────────────
    /// Devolver as primeiras 50 mil linhas e ficar quieto produziria uma planilha
    /// que parece completa e não é. Numa auditoria isso é pior que erro nenhum:
    /// quem analisa concluiria que os eventos ausentes não existiram.
    /// </summary>
    public class ExportacaoExcedeLimiteException : Exception
    {
        public ExportacaoExcedeLimiteException(long total, int limite)
            : base($"A consulta devolve {total:N0} eventos e o limite por exportação é {limite:N0}.")
        {
            Total = total;
            Limite = limite;
        }

        public long Total { get; }

        public int Limite { get; }
    }
}
