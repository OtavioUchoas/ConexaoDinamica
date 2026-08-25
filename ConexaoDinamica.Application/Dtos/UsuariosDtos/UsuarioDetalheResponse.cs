namespace ConexaoDinamica.Application.Dtos.UsuariosDtos
{
    /// <summary>
    /// Detalhe de um usuário. Como todo response deste projeto, não carrega o
    /// hash da senha — o campo simplesmente não existe aqui.
    /// </summary>
    public class UsuarioDetalheResponse
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Perfil { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
    }
}
