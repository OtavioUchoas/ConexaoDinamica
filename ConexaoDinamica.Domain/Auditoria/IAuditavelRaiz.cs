namespace ConexaoDinamica.Domain.Auditoria
{
    /// <summary>
    /// Marca uma entidade como agregado raiz para fins de auditoria: ela possui
    /// trilha própria e gera eventos independentes.
    ///
    /// O EF Core não sabe o que é agregado raiz — isso é conceito de domínio, não
    /// de mapeamento. Sem esta marcação, o interceptor não teria como distinguir
    /// um Pedido (que merece trilha) de uma tabela de apoio (que não merece), e
    /// acabaria auditando tudo que passa pelo ChangeTracker.
    ///
    /// Critério prático para decidir: faz sentido perguntar "qual o histórico
    /// desta entidade?" isoladamente? Se sim, é raiz.
    /// </summary>
    public interface IAuditavelRaiz
    {
    }
}
