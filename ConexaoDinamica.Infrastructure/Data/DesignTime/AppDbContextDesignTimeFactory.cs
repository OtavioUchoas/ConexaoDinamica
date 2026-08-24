using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConexaoDinamica.Infrastructure.Data.DesignTime
{
    /// <summary>
    /// Usada exclusivamente pelas ferramentas de linha de comando do EF (dotnet ef).
    /// Existindo esta classe, o EF a prefere e não constrói o host da aplicação —
    /// ou seja, não executa o Program.cs, não abre o LiteDB e não disputa lock do
    /// arquivo de configuração com a API em execução.
    ///
    /// A connection string daqui serve apenas para gerar e aplicar migrations em
    /// desenvolvimento. Em runtime quem determina a conexão é o IConexaoConfigStore.
    /// </summary>
    public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string VariavelAmbiente = "CONEXAODINAMICA_DESIGNTIME_CONNECTION";

        private const string ConexaoPadrao =
            "Host=localhost;Port=5432;Database=ConexaoDinamica;Username=postgres;Password=postgres";

        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable(VariavelAmbiente) ?? ConexaoPadrao;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new AppDbContext(options);
        }
    }
}
