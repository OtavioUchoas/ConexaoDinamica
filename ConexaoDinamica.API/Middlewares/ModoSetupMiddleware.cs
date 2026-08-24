using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;

namespace ConexaoDinamica.API.Middlewares
{
    /// <summary>
    /// Bloqueia as rotas de negócio enquanto as DUAS conexões — PostgreSQL e
    /// MongoDB — não estiverem configuradas.
    ///
    /// ── Por que isso existe ───────────────────────────────────────────────────
    /// A aplicação sobe sem banco de propósito (é a premissa do projeto). Sem este
    /// guard, cada endpoint de negócio falharia por conta própria, com uma exceção
    /// de conexão virando 500 "Erro interno" pelo middleware global — mensagem que
    /// não diz ao frontend o que fazer. Aqui a resposta é explícita: falta
    /// configurar, vá para o AdminCenter.
    ///
    /// ── Por que o Mongo também bloqueia ───────────────────────────────────────
    /// Auditoria é obrigatória neste sistema: operações de negócio não devem
    /// ocorrer sem deixar rastro. Permitir que a aplicação funcionasse com o
    /// Mongo ausente criaria uma janela de operações não auditadas — exatamente
    /// o que a auditoria existe para impedir.
    ///
    /// Atenção ao que este guard NÃO cobre: ele verifica se a conexão está
    /// CONFIGURADA, não se está DISPONÍVEL. Um Mongo que caia com o sistema no
    /// ar continua passando por aqui; tratar essa falha em runtime é decisão da
    /// camada de auditoria, não deste middleware.
    ///
    /// ── Por que 503 e não 403/409 ─────────────────────────────────────────────
    /// 503 Service Unavailable descreve exatamente a situação: o serviço existe,
    /// mas está temporariamente incapaz de atender por falta de uma dependência.
    /// Não é 403 (não é questão de permissão) nem 404 (a rota existe). O campo
    /// setupRequired no corpo dá ao frontend um sinal inequívoco para redirecionar,
    /// sem precisar interpretar texto de mensagem.
    ///
    /// ── Relação com o [Authorize] ─────────────────────────────────────────────
    /// Este middleware NÃO autentica nem autoriza nada — ele apenas decide se a
    /// requisição segue adiante. As rotas liberadas continuam sujeitas às suas
    /// próprias regras: /admin/login segue anônimo, /admin/conexao segue exigindo
    /// role de Administrador. São duas camadas independentes, e é essa combinação
    /// que permite configurar o sistema com segurança antes de existir banco.
    /// </summary>
    public class ModoSetupMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConexaoConfigStore _store;

        /// <summary>
        /// Prefixos que continuam acessíveis sem configuração.
        ///
        /// /api/v1/admin precisa passar por um motivo circular óbvio: é por onde a
        /// configuração é feita. Bloqueá-lo deixaria o sistema permanentemente
        /// travado, sem caminho de saída.
        /// </summary>
        private static readonly string[] PrefixosLiberados =
        [
            "/api/v1/admin",
            "/swagger",
            "/openapi"
        ];

        /// <summary>
        /// O store é singleton, e middlewares também são construídos uma única vez.
        /// Injetá-lo aqui é seguro. Um serviço scoped no construtor de um middleware
        /// seria uma captive dependency — ficaria preso à primeira requisição.
        /// </summary>
        public ModoSetupMiddleware(RequestDelegate next, IConexaoConfigStore store)
        {
            _next = next;
            _store = store;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (EhRotaLiberada(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // As duas propriedades leem do cache em memória do store, não do
            // disco. O custo por requisição é desprezível.
            var postgresOk = _store.PostgresConfigurado;
            var mongoOk = _store.MongoConfigurado;

            if (postgresOk && mongoOk)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            // Informar QUAL conexão falta, e não apenas que "falta alguma": com
            // duas dependências, uma mensagem genérica obrigaria o frontend a
            // consultar os endpoints de configuração um a um para descobrir onde
            // levar o administrador.
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = StatusCodes.Status503ServiceUnavailable,
                setupRequired = true,
                conexoes = new
                {
                    postgres = postgresOk,
                    mongo = mongoOk
                },
                message = MontarMensagem(postgresOk, mongoOk)
            });
        }

        private static string MontarMensagem(bool postgresOk, bool mongoOk)
        {
            var pendentes = new List<string>(2);

            if (!postgresOk) pendentes.Add("PostgreSQL (dados da aplicação)");
            if (!mongoOk) pendentes.Add("MongoDB (logs de auditoria)");

            return $"Configuração pendente: {string.Join(" e ", pendentes)}. " +
                   "Acesse o AdminCenter para concluir a configuração inicial.";
        }

        private static bool EhRotaLiberada(PathString caminho) =>
            PrefixosLiberados.Any(prefixo =>
                caminho.StartsWithSegments(prefixo, StringComparison.OrdinalIgnoreCase));
    }
}
