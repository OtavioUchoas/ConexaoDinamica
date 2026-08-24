namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Configuração atual do Postgres, para o AdminCenter reexibir o formulário.
    ///
    /// Deliberadamente NÃO é o mesmo tipo do request: aqui não existe campo de
    /// senha. Reaproveitar o request devolveria a senha do banco em toda chamada
    /// GET — o motivo mais concreto para request e response serem tipos distintos.
    ///
    /// No lugar da senha vai <see cref="SenhaDefinida"/>, que informa apenas se
    /// existe uma senha salva. É o suficiente para a interface decidir entre
    /// mostrar "••••••" ou um campo vazio.
    /// </summary>
    public class ConexaoPostgresResponse
    {
        public string Host { get; set; } = string.Empty;

        public int Porta { get; set; }

        public string Database { get; set; } = string.Empty;

        public string Usuario { get; set; } = string.Empty;

        /// <summary>Existe senha salva? O valor em si nunca sai do servidor.</summary>
        public bool SenhaDefinida { get; set; }

        /// <summary>Todos os campos obrigatórios estão preenchidos.</summary>
        public bool EstaCompleta { get; set; }

        public DateTime DataAtualizacao { get; set; }
    }
}
