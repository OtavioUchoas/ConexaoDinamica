namespace ConexaoDinamica.Domain.Auditoria
{
    /// <summary>
    /// Exclui uma propriedade dos snapshots e diffs de auditoria.
    ///
    /// Existe por um motivo de segurança concreto: sem ele, o snapshot completo de
    /// um Usuario levaria o SenhaHash para o Mongo, e a trilha de auditoria viraria
    /// uma cópia paralela de todos os hashes de senha do sistema — num banco que
    /// costuma ter controle de acesso mais frouxo que o principal.
    ///
    /// Use em senhas, tokens, chaves e dados pessoais sensíveis.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NaoAuditarAttribute : Attribute
    {
    }
}
