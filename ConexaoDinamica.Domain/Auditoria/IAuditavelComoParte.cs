namespace ConexaoDinamica.Domain.Auditoria
{
    /// <summary>
    /// Marca uma entidade como PARTE de um agregado: ela não tem trilha própria,
    /// e suas alterações são registradas dentro do evento do agregado raiz.
    ///
    /// A diferença em relação ao <see cref="IAuditavelRaiz"/> não é técnica, é de
    /// domínio: um ItemPedido só existe dentro de um Pedido, então perguntar "qual
    /// o histórico deste item?" isoladamente não faz sentido — o que interessa é o
    /// histórico do pedido, com as mudanças dos itens dentro dele.
    ///
    /// Auditar partes como se fossem raízes produziria uma trilha ilegível: alterar
    /// três itens de um pedido geraria quatro eventos soltos em vez de um.
    /// </summary>
    public interface IAuditavelComoParte
    {
    }
}
