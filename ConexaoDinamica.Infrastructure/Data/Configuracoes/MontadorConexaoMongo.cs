using ConexaoDinamica.Application.Configuracoes;
using MongoDB.Driver;

namespace ConexaoDinamica.Infrastructure.Data.Configuracoes
{
    /// <summary>
    /// Converte os campos separados da configuração em MongoClientSettings.
    ///
    /// Trabalhamos com MongoClientSettings em vez de montar uma URL de texto pelo
    /// mesmo motivo do NpgsqlConnectionStringBuilder no Postgres: usuário e senha
    /// entram como objeto e não precisam de escaping manual. Uma senha com '@' ou
    /// ':' quebraria uma URI mongodb:// concatenada à mão, porque esses caracteres
    /// são justamente os separadores do formato.
    /// </summary>
    internal static class MontadorConexaoMongo
    {
        /// <summary>
        /// Timeout de seleção de servidor para o teste do AdminCenter.
        ///
        /// Importante: o padrão do driver é 30 segundos. Como o MongoClient conecta
        /// de forma preguiçosa, um host errado só se revela quando o driver desiste
        /// de procurar o servidor — e o administrador ficaria meio minuto olhando
        /// para um botão travado.
        /// </summary>
        public static readonly TimeSpan TimeoutTeste = TimeSpan.FromSeconds(5);

        public static MongoClientSettings Montar(ConexaoMongoConfig config, TimeSpan? timeout = null)
        {
            var settings = new MongoClientSettings
            {
                Server = new MongoServerAddress(config.Host, config.Porta)
            };

            // Sem usuário, conecta anonimamente — cenário comum em Mongo local.
            if (!string.IsNullOrWhiteSpace(config.Usuario))
            {
                var authSource = string.IsNullOrWhiteSpace(config.AuthSource)
                    ? "admin"
                    : config.AuthSource;

                settings.Credential = MongoCredential.CreateCredential(
                    authSource, config.Usuario, config.Senha);
            }

            if (timeout.HasValue)
            {
                settings.ServerSelectionTimeout = timeout.Value;
                settings.ConnectTimeout = timeout.Value;
            }

            return settings;
        }
    }
}
