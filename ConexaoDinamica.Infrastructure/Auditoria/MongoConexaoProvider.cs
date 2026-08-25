using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.Configuracoes;
using ConexaoDinamica.Infrastructure.Data.Configuracoes;
using MongoDB.Driver;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Entrega o IMongoDatabase correspondente à configuração vigente.
    ///
    /// ── Por que não basta repetir o que foi feito no AddDbContext ─────────────
    /// No Postgres, cada requisição cria um DbContext novo e a lambda relê a
    /// configuração — o dinamismo sai de graça. Aqui não dá para fazer igual: o
    /// MongoClient é caro de criar e mantém o próprio pool de conexões, sendo
    /// projetado para viver como singleton durante toda a aplicação. Criar um por
    /// requisição vazaria pools e degradaria o desempenho.
    ///
    /// A saída é este provider: guarda um único client e o recria apenas quando a
    /// configuração muda de fato. A comparação é feita sobre os campos que afetam
    /// a conexão — DataAtualizacao não serve, porque um "salvar" sem alteração
    /// nenhuma recriaria o client à toa.
    /// </summary>
    public class MongoConexaoProvider : IMongoConexaoProvider
    {
        private readonly IConexaoConfigStore _store;
        private readonly object _lock = new();

        private MongoClient? _client;
        private string? _chaveAtual;
        private string? _databaseAtual;

        public MongoConexaoProvider(IConexaoConfigStore store)
        {
            _store = store;
        }

        public IMongoDatabase? ObterDatabase()
        {
            var config = _store.ObterMongo();

            if (config is null || !config.EstaCompleta)
                return null;

            var chave = MontarChave(config);

            lock (_lock)
            {
                if (_client is null || _chaveAtual != chave)
                {
                    // O client antigo não é descartado explicitamente: pode haver
                    // operação em voo usando-o. O driver encerra as conexões
                    // ociosas por conta própria, e trocas de configuração são
                    // raras o suficiente para que isso não acumule.
                    _client = new MongoClient(MontadorConexaoMongo.Montar(config));
                    _chaveAtual = chave;
                    _databaseAtual = config.Database;
                }

                return _client.GetDatabase(_databaseAtual);
            }
        }

        /// <summary>
        /// Identidade da conexão. Inclui a senha porque trocá-la exige um client
        /// novo — as credenciais são resolvidas na construção, não a cada operação.
        /// </summary>
        private static string MontarChave(ConexaoMongoConfig config) =>
            string.Join('|', config.Host, config.Porta, config.Database,
                             config.Usuario, config.Senha, config.AuthSource);
    }
}
