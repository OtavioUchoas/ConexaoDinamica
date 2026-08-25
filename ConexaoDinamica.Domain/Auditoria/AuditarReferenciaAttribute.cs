namespace ConexaoDinamica.Domain.Auditoria
{
    /// <summary>
    /// Declara que uma chave estrangeira deve ser gravada na auditoria junto de uma
    /// descrição legível, e não apenas como número.
    ///
    /// ── O problema que resolve ────────────────────────────────────────────────
    /// "ClienteId: 5" é rastreável mas não é informativo. Daqui a um ano, quem ler
    /// a trilha não saberá quem era o cliente 5 — ele pode ter sido renomeado,
    /// desativado ou mesclado. O log perde justamente o significado que tinha no
    /// momento do fato, que é a razão de a auditoria existir.
    ///
    /// A solução é desnormalizar o mínimo: guarda-se o id (rastreabilidade) e uma
    /// descrição do momento (significado histórico), sem copiar a entidade inteira.
    ///
    /// É deliberadamente explícito: cada referência declarada custa uma consulta ao
    /// gravar o evento, então declare poucas — só as que realmente dão sentido.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AuditarReferenciaAttribute : Attribute
    {
        public AuditarReferenciaAttribute(Type tipoReferenciado, string propriedadeDescricao)
        {
            TipoReferenciado = tipoReferenciado;
            PropriedadeDescricao = propriedadeDescricao;
        }

        /// <summary>Entidade apontada pela chave estrangeira.</summary>
        public Type TipoReferenciado { get; }

        /// <summary>Propriedade usada como descrição legível (ex.: "Nome").</summary>
        public string PropriedadeDescricao { get; }
    }
}
