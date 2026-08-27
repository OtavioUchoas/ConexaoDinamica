using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.Auditoria;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ConexaoDinamica.Infrastructure.Auditoria
{
    /// <summary>
    /// Grava e consulta a trilha de auditoria no MongoDB.
    ///
    /// ── Política de falha: assimétrica de propósito ───────────────────────────
    /// ESCRITA engole a falha: se o Mongo estiver indisponível, a operação de
    /// negócio não é interrompida — o erro vai para o log e o fluxo segue. É uma
    /// decisão consciente, com consequência real: numa queda do Mongo, eventos são
    /// perdidos em definitivo. A alternativa correta, quando perder evento for
    /// inaceitável, é o padrão Outbox — gravar o evento no Postgres dentro da
    /// MESMA transação da alteração e publicá-lo depois por um worker.
    ///
    /// LEITURA propaga a falha: quem consulta precisa saber que o resultado não
    /// veio. Devolver lista vazia seria indistinguível de "nenhum evento
    /// encontrado", e uma trilha que parece vazia por engano é pior que um erro.
    /// </summary>
    public class MongoAuditoriaRepository : IAuditoriaRepository
    {
        private const string NomeColecao = "eventos_auditoria";

        private readonly IMongoConexaoProvider _provider;
        private readonly ILogger<MongoAuditoriaRepository> _logger;

        public MongoAuditoriaRepository(
            IMongoConexaoProvider provider,
            ILogger<MongoAuditoriaRepository> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task RegistrarAsync(
            IReadOnlyList<EventoAuditoria> eventos,
            CancellationToken cancellationToken = default)
        {
            if (eventos.Count == 0)
                return;

            try
            {
                var colecao = ObterColecao();

                if (colecao is null)
                {
                    // Não deveria acontecer: o modo setup impede a aplicação de
                    // operar sem o Mongo configurado. Registrado como aviso porque,
                    // se ocorrer, indica falha naquele guard.
                    _logger.LogWarning(
                        "Auditoria ignorada: MongoDB não configurado. {Quantidade} evento(s) perdido(s).",
                        eventos.Count);
                    return;
                }

                await colecao.InsertManyAsync(eventos, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Nível Error, e não Warning: perder trilha de auditoria é um
                // problema que alguém precisa investigar, mesmo que a operação de
                // negócio tenha seguido normalmente.
                _logger.LogError(ex,
                    "Falha ao gravar auditoria no MongoDB. {Quantidade} evento(s) perdido(s). " +
                    "Entidades: {Entidades}",
                    eventos.Count,
                    string.Join(", ", eventos.Select(e => $"{e.Entidade.Tipo}#{e.Entidade.Id}")));
            }
        }

        public async Task<ResultadoPaginado<EventoAuditoria>> ConsultarAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default)
        {
            var colecao = ObterColecao()
                ?? throw new InvalidOperationException("MongoDB não configurado.");

            var pagina = Math.Max(1, filtro.Pagina);

            // O limite é aplicado no servidor: confiar no tamanho enviado pelo
            // cliente permitiria pedir cem mil registros numa requisição.
            var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, FiltroAuditoria.TamanhoMaximoPagina);

            var condicao = MontarCondicao(filtro);

            // Contagem e busca em paralelo: são consultas independentes, e em série
            // a resposta levaria a soma dos dois tempos.
            var contagem = colecao.CountDocumentsAsync(condicao, cancellationToken: cancellationToken);

            var busca = colecao
                .Find(condicao)
                .Sort(Builders<EventoAuditoria>.Sort.Descending(e => e.DataHora))
                .Skip((pagina - 1) * tamanho)
                .Limit(tamanho)
                .ToListAsync(cancellationToken);

            await Task.WhenAll(contagem, busca);

            return new ResultadoPaginado<EventoAuditoria>
            {
                Itens = busca.Result,
                Total = contagem.Result,
                Pagina = pagina,
                TamanhoPagina = tamanho,
            };
        }

        public async Task<IReadOnlyList<EventoAuditoria>> ConsultarParaExportacaoAsync(
            FiltroAuditoria filtro,
            CancellationToken cancellationToken = default)
        {
            var colecao = ObterColecao()
                ?? throw new InvalidOperationException("MongoDB não configurado.");

            var condicao = MontarCondicao(filtro);

            // Conta ANTES de buscar. Trazer os documentos para depois descobrir que
            // são demais já teria custado a memória que o limite existe para
            // proteger — a contagem resolve no índice, sem materializar nada.
            var total = await colecao.CountDocumentsAsync(condicao, cancellationToken: cancellationToken);

            if (total > FiltroAuditoria.LimiteExportacao)
            {
                throw new ExportacaoExcedeLimiteException(total, FiltroAuditoria.LimiteExportacao);
            }

            // Ordem crescente aqui, ao contrário da consulta da tela. Quem lê a
            // trilha na interface quer o que acabou de acontecer no topo; quem abre
            // a planilha quer acompanhar a história na ordem em que ela ocorreu.
            return await colecao
                .Find(condicao)
                .Sort(Builders<EventoAuditoria>.Sort.Ascending(e => e.DataHora))
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<string>> ObterTiposEntidadeAsync(
            CancellationToken cancellationToken = default)
        {
            var colecao = ObterColecao()
                ?? throw new InvalidOperationException("MongoDB não configurado.");

            var tipos = await colecao.DistinctAsync<string>(
                "Entidade.Tipo",
                Builders<EventoAuditoria>.Filter.Empty,
                cancellationToken: cancellationToken);

            return (await tipos.ToListAsync(cancellationToken)).Order().ToList();
        }

        /// <summary>
        /// Traduz o filtro em condição do Mongo.
        ///
        /// Cada critério só entra quando informado — montar a condição com campos
        /// vazios excluiria todos os documentos em vez de ignorar o critério.
        /// </summary>
        private static FilterDefinition<EventoAuditoria> MontarCondicao(FiltroAuditoria filtro)
        {
            var construtor = Builders<EventoAuditoria>.Filter;
            var condicoes = new List<FilterDefinition<EventoAuditoria>>();

            if (!string.IsNullOrWhiteSpace(filtro.TipoEntidade))
                condicoes.Add(construtor.Eq(e => e.Entidade.Tipo, filtro.TipoEntidade));

            if (!string.IsNullOrWhiteSpace(filtro.EntidadeId))
                condicoes.Add(construtor.Eq(e => e.Entidade.Id, filtro.EntidadeId));

            if (filtro.TipoEvento.HasValue)
                condicoes.Add(construtor.Eq(e => e.TipoEvento, filtro.TipoEvento.Value));

            if (!string.IsNullOrWhiteSpace(filtro.UsuarioId))
                condicoes.Add(construtor.Eq("Usuario.Id", filtro.UsuarioId));

            if (filtro.DataInicio.HasValue)
                condicoes.Add(construtor.Gte(e => e.DataHora, filtro.DataInicio.Value));

            if (filtro.DataFim.HasValue)
                condicoes.Add(construtor.Lte(e => e.DataHora, filtro.DataFim.Value));

            return condicoes.Count == 0 ? construtor.Empty : construtor.And(condicoes);
        }

        private IMongoCollection<EventoAuditoria>? ObterColecao() =>
            _provider.ObterDatabase()?.GetCollection<EventoAuditoria>(NomeColecao);
    }
}
