namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Resultado da aplicação de migrations no banco configurado.
    /// </summary>
    public class AplicarMigrationsResponse
    {
        public bool Sucesso { get; set; }

        public string Mensagem { get; set; } = string.Empty;

        /// <summary>
        /// Migrations aplicadas nesta execução. Vem vazio quando o banco já
        /// estava atualizado — o que é sucesso, e não falha.
        /// </summary>
        public IReadOnlyList<string> Aplicadas { get; set; } = [];
    }
}
