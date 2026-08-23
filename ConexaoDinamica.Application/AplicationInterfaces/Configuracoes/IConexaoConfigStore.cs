using ConexaoDinamica.Application.Configuracoes;

namespace ConexaoDinamica.Application.AplicationInterfaces.Configuracoes
{
    /// <summary>
    /// Armazenamento das configurações de conexão.
    /// API síncrona de propósito: a lambda do AddDbContext é síncrona, e
    /// bloquear um método async ali dentro seria pedir deadlock.
    /// </summary>
    public interface IConexaoConfigStore
    {
        ConexaoPostgresConfig? ObterPostgres();

        void SalvarPostgres(ConexaoPostgresConfig config);

        bool PostgresConfigurado { get; }
    }
}
