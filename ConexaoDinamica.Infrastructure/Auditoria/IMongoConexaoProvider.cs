using MongoDB.Driver;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Interna à Infrastructure de propósito: expõe IMongoDatabase, um tipo do
    /// driver, e portanto não pode aparecer em contrato da Application.
    /// </summary>
    public interface IMongoConexaoProvider
    {
        /// <summary>
        /// Database vigente, ou null quando o Mongo ainda não foi configurado.
        /// </summary>
        IMongoDatabase? ObterDatabase();
    }
}
