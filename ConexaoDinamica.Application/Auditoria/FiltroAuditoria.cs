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
    }
}
