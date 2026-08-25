using ConexaoDinamica.Domain.Auditoria;

namespace ConexaoDinamica.Domain.Entidades.Clientes
{
    /// <summary>
    /// Agregado raiz próprio: o cliente existe e muda independentemente de
    /// qualquer pedido, então tem trilha de auditoria própria.
    ///
    /// Nos eventos de Pedido ele aparece apenas como referência (id + nome), nunca
    /// como objeto completo — quem quiser o histórico do cliente consulta a trilha
    /// dele. Auditar o cliente inteiro dentro de cada pedido duplicaria informação
    /// e faria uma simples troca de nome gerar eventos em centenas de pedidos.
    /// </summary>
    public class Cliente : IAuditavelRaiz
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime DataCadastro { get; set; }
    }
}
