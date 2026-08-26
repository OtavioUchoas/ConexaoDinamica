namespace ConexaoDinamica.Application.Dtos.ClientesDtos
{
    public class ClienteResponse
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime DataCadastro { get; set; }
    }
}
