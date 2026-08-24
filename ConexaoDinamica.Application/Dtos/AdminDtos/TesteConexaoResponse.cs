namespace ConexaoDinamica.Application.Dtos.AdminDtos
{
    /// <summary>
    /// Resultado de um teste de conexão.
    ///
    /// Note que falhar em conectar NÃO é erro de API: a resposta é 200 com
    /// <see cref="Sucesso"/> = false. O endpoint funcionou perfeitamente e a
    /// pergunta ("dá para conectar?") foi respondida — a resposta é "não".
    /// Devolver 500 aqui confundiria falha de infraestrutura do servidor com
    /// uma credencial digitada errada pelo administrador.
    /// </summary>
    public class TesteConexaoResponse
    {
        public bool Sucesso { get; set; }

        /// <summary>
        /// Mensagem para o administrador. Em caso de falha traz o erro real do
        /// Npgsql (host desconhecido, autenticação recusada, banco inexistente),
        /// que é justamente o que permite corrigir o formulário.
        /// </summary>
        public string Mensagem { get; set; } = string.Empty;

        /// <summary>Tempo até abrir a conexão. Útil para perceber latência alta.</summary>
        public long TempoMs { get; set; }

        /// <summary>Versão do servidor Postgres. Só preenchido quando conecta.</summary>
        public string? VersaoServidor { get; set; }
    }
}
