using ConexaoDinamica.Application.Dtos.AdminDtos;

namespace ConexaoDinamica.Application.AplicationInterfaces.Configuracoes
{
    /// <summary>
    /// Casos de uso do AdminCenter para as conexões de banco.
    ///
    /// Diferente do <see cref="IConexaoConfigStore"/>, aqui a API é assíncrona:
    /// o store é síncrono porque é consumido dentro da lambda do AddDbContext
    /// (que é síncrona), enquanto estes métodos são chamados por controllers,
    /// onde I/O de rede e banco deve ser aguardado sem bloquear thread.
    ///
    /// A implementação vive na Infrastructure porque precisa do Npgsql, do
    /// EF Core e do driver do Mongo — dependências que a Application não conhece.
    /// </summary>
    public interface IConexaoAdminService
    {
        // ── Postgres ───────────────────────────────────────────────────────────

        /// <summary>
        /// Configuração salva, sem a senha. Retorna null quando nunca foi configurado.
        /// </summary>
        ConexaoPostgresResponse? ObterConfiguracao();

        /// <summary>
        /// Tenta abrir uma conexão com os dados informados, SEM salvar nada.
        /// É o "Testar conexão" do formulário: permite validar antes de gravar.
        /// </summary>
        Task<TesteConexaoResponse> TestarAsync(ConexaoPostgresRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persiste a configuração. A partir da próxima requisição, todo
        /// AppDbContext criado já usa esta conexão — sem reiniciar a aplicação.
        /// </summary>
        ConexaoPostgresResponse Salvar(ConexaoPostgresRequest request);

        /// <summary>
        /// Migrations aplicadas e pendentes no banco atualmente configurado.
        /// </summary>
        Task<StatusMigrationsResponse> ObterStatusMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Aplica as migrations pendentes. Equivale ao `dotnet ef database update`,
        /// disparado pelo painel — substitui o Migrate() que antes rodava no startup.
        /// </summary>
        Task<AplicarMigrationsResponse> AplicarMigrationsAsync(CancellationToken cancellationToken = default);

        // ── MongoDB ────────────────────────────────────────────────────────────

        /// <summary>
        /// Configuração salva do Mongo, sem a senha. Null quando nunca configurado.
        /// </summary>
        ConexaoMongoResponse? ObterConfiguracaoMongo();

        /// <summary>
        /// Testa a conexão com o Mongo sem salvar nada.
        /// </summary>
        Task<TesteConexaoResponse> TestarMongoAsync(ConexaoMongoRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persiste a configuração do Mongo.
        /// </summary>
        ConexaoMongoResponse SalvarMongo(ConexaoMongoRequest request);
    }
}
