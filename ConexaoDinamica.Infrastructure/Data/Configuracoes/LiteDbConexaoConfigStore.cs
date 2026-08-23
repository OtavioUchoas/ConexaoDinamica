using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.Configuracoes;
using LiteDB;

namespace ConexaoDinamica.Infrastructure.Data.Configuracoes
{
    /// <summary>
    /// Store das configurações de conexão em LiteDB.
    /// Singleton com cache em memória: a lambda do AddDbContext roda a cada
    /// scope, então ler o arquivo em toda requisição seria I/O desnecessário.
    /// </summary>
    public class LiteDbConexaoConfigStore : IConexaoConfigStore
    {
        private const string ColecaoPostgres = "conexao_postgres";

        private readonly LiteDatabase _db;
        private readonly object _lock = new();

        private ConexaoPostgresConfig? _cachePostgres;
        private bool _cachePostgresCarregado;

        public LiteDbConexaoConfigStore(LiteDatabase db)
        {
            _db = db;
        }

        public bool PostgresConfigurado => ObterPostgres()?.EstaCompleta ?? false;

        public ConexaoPostgresConfig? ObterPostgres()
        {
            lock (_lock)
            {
                if (_cachePostgresCarregado)
                    return _cachePostgres;

                _cachePostgres = _db
                    .GetCollection<ConexaoPostgresConfig>(ColecaoPostgres)
                    .FindById(1);

                _cachePostgresCarregado = true;
                return _cachePostgres;
            }
        }

        public void SalvarPostgres(ConexaoPostgresConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            lock (_lock)
            {
                config.Id = 1;
                config.DataAtualizacao = DateTime.UtcNow;

                _db.GetCollection<ConexaoPostgresConfig>(ColecaoPostgres).Upsert(config);

                // Atualiza o cache no mesmo lock: a próxima leitura já vê o valor novo,
                // que é o que torna a troca de conexão efetiva sem reiniciar a aplicação.
                _cachePostgres = config;
                _cachePostgresCarregado = true;
            }
        }
    }
}
