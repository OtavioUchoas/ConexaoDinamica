namespace ConexaoDinamica.Application.Auditoria
{
    /// <summary>
    /// Critérios de consulta da trilha de auditoria.
    ///
    /// Todos os campos são opcionais e se combinam: informar tipo de entidade e
    /// período restringe pelos dois. Ausência de filtro devolve os eventos mais
    /// recentes.
    /// </summary>
    public class FiltroAuditoria
    {
        /// <summary>Nome da entidade — "Usuario", "Pedido", "Cliente".</summary>
        public string? TipoEntidade { get; set; }

        /// <summary>Registro específico. Só faz sentido junto de TipoEntidade.</summary>
        public string? EntidadeId { get; set; }

        public TipoEventoAuditoria? TipoEvento { get; set; }

        public string? UsuarioId { get; set; }

        public DateTime? DataInicio { get; set; }

        public DateTime? DataFim { get; set; }

        public int Pagina { get; set; } = 1;

        /// <summary>
        /// Limitado no servidor, não aqui: um cliente poderia pedir 100 mil
        /// registros de uma vez e derrubar a resposta. Ver TamanhoMaximoPagina.
        /// </summary>
        public int TamanhoPagina { get; set; } = 25;

        public const int TamanhoMaximoPagina = 100;

        /// <summary>
        /// Teto de linhas de uma exportação.
        ///
        /// A exportação ignora a paginação de propósito — quem exporta quer o
        /// resultado inteiro do filtro, não a página que está na tela. Mas sem
        /// teto, um filtro vazio numa trilha madura tentaria montar uma planilha
        /// com milhões de linhas em memória, e o processo morre antes de
        /// responder. Estourado o limite, a resposta orienta a estreitar o
        /// período em vez de devolver um arquivo truncado em silêncio.
        /// </summary>
        public const int LimiteExportacao = 50_000;

        /// <summary>
        /// Descreve os critérios em uma linha legível, para registrar na trilha o
        /// QUE foi exportado. Sem isso, o evento de exportação diria apenas que
        /// alguém exportou algo, o que não serve para auditar coisa nenhuma.
        /// </summary>
        public string Descrever()
        {
            var partes = new List<string>();

            if (!string.IsNullOrWhiteSpace(TipoEntidade))
                partes.Add($"entidade={TipoEntidade}");

            if (!string.IsNullOrWhiteSpace(EntidadeId))
                partes.Add($"registro={EntidadeId}");

            if (TipoEvento is not null)
                partes.Add($"evento={TipoEvento}");

            if (!string.IsNullOrWhiteSpace(UsuarioId))
                partes.Add($"usuario={UsuarioId}");

            if (DataInicio is not null)
                partes.Add($"de={DataInicio:yyyy-MM-dd HH:mm}");

            if (DataFim is not null)
                partes.Add($"ate={DataFim:yyyy-MM-dd HH:mm}");

            return partes.Count == 0 ? "sem filtro" : string.Join(", ", partes);
        }
    }
}
