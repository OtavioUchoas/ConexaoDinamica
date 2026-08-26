namespace ConexaoDinamica.Application.Dtos.ClientesDtos
{
    /// <summary>
    /// Dados de entrada de cliente. Serve para criação e edição — os campos
    /// aceitos são os mesmos, e o id vem pela rota, não pelo corpo.
    /// </summary>
    public class ClienteRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Documento { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
