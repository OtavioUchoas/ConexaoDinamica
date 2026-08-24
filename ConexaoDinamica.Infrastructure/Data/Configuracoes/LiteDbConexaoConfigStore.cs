using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.Configuracoes;
using LiteDB;

namespace ConexaoDinamica.Infrastructure.Data.Configuracoes
{
    /// <summary>
    /// Store das configurações de conexão em LiteDB.
    /// Singleton com cache em memória: a lambda do AddDbContext roda a cada
    /// scope, então ler o arquivo em toda requisição seria I/O desnecessário.
    ///
    /// Cada tipo de conexão fica em sua própria coleção, com documento único
    /// (Id = 1). Coleções separadas em vez de um documento genérico porque os
    /// campos divergem — o Mongo tem AuthSource, o Postgres não — e tipar cada
    /// um evita um saco de chave/valor sem validação.
    /// </summary>
    public class LiteDbConexaoConfigStore : IConexaoConfigStore
    {
        private const string ColecaoPostgres = "conexao_postgres";
        private const string ColecaoMongo = "conexao_mongo";

        private readonly LiteDatabase _db;
        private readonly object _lock = new();

        private ConexaoPostgresConfig? _cachePostgres;
        private bool _cachePostgresCarregado;

        private ConexaoMongoConfig? _cacheMongo;
        private bool _cacheMongoCarregado;

        public LiteDbConexaoConfigStore(LiteDatabase db)
        {
            _db = db;
        }

        // ── Postgres ───────────────────────────────────────────────────────────

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

        // ── MongoDB ────────────────────────────────────────────────────────────

        public bool MongoConfigurado => ObterMongo()?.EstaCompleta ?? false;

        public ConexaoMongoConfig? ObterMongo()
        {
            lock (_lock)
            {
                if (_cacheMongoCarregado)
                    return _cacheMongo;

                _cacheMongo = _db
                    .GetCollection<ConexaoMongoConfig>(ColecaoMongo)
                    .FindById(1);

                _cacheMongoCarregado = true;
                return _cacheMongo;
            }
        }

        public void SalvarMongo(ConexaoMongoConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            lock (_lock)
            {
                config.Id = 1;
                config.DataAtualizacao = DateTime.UtcNow;

                _db.GetCollection<ConexaoMongoConfig>(ColecaoMongo).Upsert(config);

                _cacheMongo = config;
                _cacheMongoCarregado = true;
            }
        }
    }
}
