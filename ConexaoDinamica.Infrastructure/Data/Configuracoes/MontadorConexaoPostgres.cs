using ConexaoDinamica.Application.Configuracoes;
using Npgsql;

namespace ConexaoDinamica.Infrastructure.Data.Configuracoes
{
    /// <summary>
    /// Converte os campos separados da configuração em uma connection string.
    ///
    /// Usa NpgsqlConnectionStringBuilder e nunca interpolação de string. O motivo
    /// é concreto: uma senha como "p@ss;w=rd" concatenada à mão produz
    ///
    ///     ...;Password=p@ss;w=rd
    ///
    /// onde o ';' encerra o parâmetro e "w=rd" vira uma chave inexistente. O erro
    /// resultante ("Couldn't set w") não menciona senha nem conexão. O builder
    /// resolve colocando aspas: Password="p@ss;w=rd".
    /// </summary>
    internal static class MontadorConexaoPostgres
    {
        /// <summary>
        /// Timeout curto para o teste de conexão do AdminCenter. O padrão do
        /// Npgsql (15s) deixaria o administrador esperando demais diante de um
        /// host digitado errado, que é o caso mais comum de falha no formulário.
        /// </summary>
        public const int TimeoutTesteSegundos = 5;

        /// <param name="timeoutSegundos">
        /// Quando informado, limita o tempo de espera para abrir a conexão.
        /// Omitido no uso normal da aplicação, onde vale o padrão do Npgsql.
        /// </param>
        public static string Montar(ConexaoPostgresConfig config, int? timeoutSegundos = null)
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = config.Host,
                Port = config.Porta,
                Database = config.Database,
                Username = config.Usuario,
                Password = config.Senha
            };

            if (timeoutSegundos.HasValue)
                builder.Timeout = timeoutSegundos.Value;

            return builder.ConnectionString;
        }
    }
}
