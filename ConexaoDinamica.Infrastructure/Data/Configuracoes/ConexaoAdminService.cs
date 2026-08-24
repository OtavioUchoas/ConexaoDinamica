using System.Diagnostics;
using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.Configuracoes;
using ConexaoDinamica.Application.Dtos.AdminDtos;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using Npgsql;

namespace ConexaoDinamica.Infrastructure.Data.Configuracoes
{
    /// <summary>
    /// Implementa os casos de uso do AdminCenter para a conexão do Postgres.
    ///
    /// ── Por que este serviço NÃO injeta AppDbContext ──────────────────────────
    /// Seria o caminho natural, mas quebraria em um cenário específico e comum:
    /// salvar uma configuração nova e aplicar migrations na mesma requisição.
    ///
    /// O AppDbContext é scoped, então sua connection string é resolvida uma única
    /// vez, quando o scope da requisição cria a instância. Se ele fosse injetado
    /// no construtor, estaria preso à configuração vigente no INÍCIO da requisição
    /// — a antiga. As migrations iriam para o banco errado, silenciosamente.
    ///
    /// Por isso cada operação constrói seu próprio contexto, a partir da
    /// configuração lida naquele instante. Custa pouco (abrir um DbContext é
    /// barato) e elimina a ambiguidade.
    /// </summary>
    public class ConexaoAdminService : IConexaoAdminService
    {
        private readonly IConexaoConfigStore _store;

        public ConexaoAdminService(IConexaoConfigStore store)
        {
            _store = store;
        }

        public ConexaoPostgresResponse? ObterConfiguracao()
        {
            var config = _store.ObterPostgres();
            return config is null ? null : MapearParaResponse(config);
        }

        public async Task<TesteConexaoResponse> TestarAsync(
            ConexaoPostgresRequest request,
            CancellationToken cancellationToken = default)
        {
            // Testa os dados RECEBIDOS, não os salvos: é o que permite validar
            // o formulário antes de gravar qualquer coisa.
            var config = MapearParaConfig(request);
            var connectionString = MontadorConexaoPostgres.Montar(
                config, MontadorConexaoPostgres.TimeoutTesteSegundos);

            var cronometro = Stopwatch.StartNew();

            try
            {
                await using var conexao = new NpgsqlConnection(connectionString);
                await conexao.OpenAsync(cancellationToken);

                cronometro.Stop();

                return new TesteConexaoResponse
                {
                    Sucesso = true,
                    Mensagem = "Conexão estabelecida com sucesso.",
                    TempoMs = cronometro.ElapsedMilliseconds,
                    VersaoServidor = conexao.PostgreSqlVersion.ToString()
                };
            }
            catch (Exception ex)
            {
                cronometro.Stop();

                // Falha de conexão é uma resposta válida deste endpoint, não uma
                // exceção a propagar: quem digitou o dado errado foi o usuário, e
                // ele precisa ler o motivo. Deixar subir viraria 500 pelo
                // middleware global, com a mensagem genérica "Erro interno".
                return new TesteConexaoResponse
                {
                    Sucesso = false,
                    Mensagem = DescreverFalha(ex),
                    TempoMs = cronometro.ElapsedMilliseconds
                };
            }
        }

        public ConexaoPostgresResponse Salvar(ConexaoPostgresRequest request)
        {
            var config = MapearParaConfig(request);

            // Salvar sem exigir teste prévio é intencional: é legítimo configurar
            // um banco que ainda não subiu. Quem decide se testa antes é a
            // interface, não o servidor.
            _store.SalvarPostgres(config);

            // Relê do store para devolver o DataAtualizacao que ele carimbou.
            var salva = _store.ObterPostgres()!;
            return MapearParaResponse(salva);
        }

        public async Task<StatusMigrationsResponse> ObterStatusMigrationsAsync(
            CancellationToken cancellationToken = default)
        {
            var config = _store.ObterPostgres();

            if (config is null || !config.EstaCompleta)
            {
                return new StatusMigrationsResponse
                {
                    Configurado = false,
                    ConseguiuConectar = false,
                    Erro = "Nenhuma conexão configurada."
                };
            }

            try
            {
                await using var contexto = CriarContexto(config);

                // GetAppliedMigrations consulta a tabela __EFMigrationsHistory no
                // banco; GetPendingMigrations compara com o que existe no assembly.
                var aplicadas = await contexto.Database.GetAppliedMigrationsAsync(cancellationToken);
                var pendentes = await contexto.Database.GetPendingMigrationsAsync(cancellationToken);

                return new StatusMigrationsResponse
                {
                    Configurado = true,
                    ConseguiuConectar = true,
                    Aplicadas = aplicadas.ToList(),
                    Pendentes = pendentes.ToList()
                };
            }
            catch (Exception ex)
            {
                return new StatusMigrationsResponse
                {
                    Configurado = true,
                    ConseguiuConectar = false,
                    Erro = DescreverFalha(ex)
                };
            }
        }

        public async Task<AplicarMigrationsResponse> AplicarMigrationsAsync(
            CancellationToken cancellationToken = default)
        {
            var config = _store.ObterPostgres();

            if (config is null || !config.EstaCompleta)
            {
                return new AplicarMigrationsResponse
                {
                    Sucesso = false,
                    Mensagem = "Nenhuma conexão configurada."
                };
            }

            try
            {
                await using var contexto = CriarContexto(config);

                // Capturado ANTES de migrar: depois do Migrate a lista de
                // pendentes fica vazia, e não haveria como informar o que foi feito.
                var pendentes = (await contexto.Database
                    .GetPendingMigrationsAsync(cancellationToken)).ToList();

                if (pendentes.Count == 0)
                {
                    return new AplicarMigrationsResponse
                    {
                        Sucesso = true,
                        Mensagem = "O banco já está atualizado. Nenhuma migration pendente."
                    };
                }

                // Migrate() cria o banco caso ele ainda não exista.
                await contexto.Database.MigrateAsync(cancellationToken);

                return new AplicarMigrationsResponse
                {
                    Sucesso = true,
                    Mensagem = $"{pendentes.Count} migration(s) aplicada(s) com sucesso.",
                    Aplicadas = pendentes
                };
            }
            catch (Exception ex)
            {
                return new AplicarMigrationsResponse
                {
                    Sucesso = false,
                    Mensagem = DescreverFalha(ex)
                };
            }
        }

        // ── MongoDB ────────────────────────────────────────────────────────────

        public ConexaoMongoResponse? ObterConfiguracaoMongo()
        {
            var config = _store.ObterMongo();
            return config is null ? null : MapearParaResponse(config);
        }

        public async Task<TesteConexaoResponse> TestarMongoAsync(
            ConexaoMongoRequest request,
            CancellationToken cancellationToken = default)
        {
            var config = MapearParaConfig(request);
            var settings = MontadorConexaoMongo.Montar(config, MontadorConexaoMongo.TimeoutTeste);

            var cronometro = Stopwatch.StartNew();

            try
            {
                var client = new MongoClient(settings);
                var database = client.GetDatabase(config.Database);

                // Criar o MongoClient NÃO conecta em nada: o driver é preguiçoso e
                // só busca o servidor quando há uma operação real. Sem executar um
                // comando, este teste diria "conectado" com o Mongo desligado.
                // O ping é o comando canônico para forçar essa verificação.
                await database.RunCommandAsync<BsonDocument>(
                    new BsonDocument("ping", 1), cancellationToken: cancellationToken);

                cronometro.Stop();

                // buildInfo traz a versão do servidor. Falhar aqui não invalida o
                // teste — a conexão já foi provada pelo ping —, então a versão é
                // apenas informativa.
                string? versao = null;
                try
                {
                    var buildInfo = await database.RunCommandAsync<BsonDocument>(
                        new BsonDocument("buildInfo", 1), cancellationToken: cancellationToken);
                    versao = buildInfo.GetValue("version", BsonNull.Value)?.ToString();
                }
                catch
                {
                    // Usuário sem permissão para buildInfo: segue sem a versão.
                }

                return new TesteConexaoResponse
                {
                    Sucesso = true,
                    Mensagem = "Conexão estabelecida com sucesso.",
                    TempoMs = cronometro.ElapsedMilliseconds,
                    VersaoServidor = versao
                };
            }
            catch (Exception ex)
            {
                cronometro.Stop();

                return new TesteConexaoResponse
                {
                    Sucesso = false,
                    Mensagem = DescreverFalhaMongo(ex, config),
                    TempoMs = cronometro.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Traduz falhas do driver do Mongo em mensagens acionáveis.
        ///
        /// Diferente do Npgsql, que devolve mensagens curtas e diretas, o driver do
        /// Mongo embute um dump completo do estado do cluster — com stack trace —
        /// dentro da própria mensagem de timeout. São mais de mil caracteres
        /// inúteis para quem está preenchendo um formulário, e que ainda expõem
        /// detalhes internos do driver ao cliente.
        ///
        /// Por isso mapeamos por TIPO de exceção e escrevemos a mensagem nós
        /// mesmos, em vez de repassar o texto do driver.
        /// </summary>
        private static string DescreverFalhaMongo(Exception ex, ConexaoMongoConfig config)
        {
            var destino = $"{config.Host}:{config.Porta}";

            return ex switch
            {
                TimeoutException =>
                    $"Não foi possível conectar ao MongoDB em {destino}. " +
                    "Verifique se o servidor está em execução e acessível.",

                // A dica sobre o "admin" só aparece quando o valor configurado é
                // outro. Repeti-la quando já está correto ("atualmente admin,
                // normalmente é admin") só confundiria quem lê.
                MongoAuthenticationException =>
                    "Falha na autenticação. Verifique usuário, senha e o AuthSource" +
                    (string.Equals(config.AuthSource, "admin", StringComparison.OrdinalIgnoreCase)
                        ? "."
                        : $" (configurado como \"{config.AuthSource}\" — normalmente é \"admin\")."),

                MongoCommandException cmd =>
                    $"O servidor recusou o comando: {PrimeiraLinha(cmd.Message)}",

                MongoConnectionException =>
                    $"Falha ao abrir conexão com {destino}. {PrimeiraLinha(DescreverFalha(ex))}",

                _ => PrimeiraLinha(DescreverFalha(ex))
            };
        }

        /// <summary>
        /// Primeira linha da mensagem, limitada em tamanho. Evita que stack traces
        /// e dumps multi-linha vazem para a resposta HTTP.
        /// </summary>
        private static string PrimeiraLinha(string mensagem, int limite = 300)
        {
            var linha = mensagem.Split('\n')[0].Trim();
            return linha.Length <= limite ? linha : linha[..limite] + "...";
        }

        public ConexaoMongoResponse SalvarMongo(ConexaoMongoRequest request)
        {
            var config = MapearParaConfig(request);
            _store.SalvarMongo(config);

            var salva = _store.ObterMongo()!;
            return MapearParaResponse(salva);
        }

        /// <summary>
        /// Cria um contexto amarrado à configuração informada, sem passar pelo DI.
        /// Ver a nota no topo da classe sobre por que não usamos o contexto injetado.
        /// </summary>
        private static AppDbContext CriarContexto(ConexaoPostgresConfig config)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(MontadorConexaoPostgres.Montar(config))
                .Options;

            return new AppDbContext(options);
        }

        /// <summary>
        /// Extrai a mensagem mais específica disponível. Erros do Npgsql costumam
        /// vir embrulhados, e a exceção externa é genérica demais para ajudar
        /// ("An error occurred..."); a interna é que diz "host desconhecido" ou
        /// "autenticação de senha falhou".
        /// </summary>
        private static string DescreverFalha(Exception ex)
        {
            var raiz = ex;
            while (raiz.InnerException is not null)
                raiz = raiz.InnerException;

            return raiz.Message;
        }

        private static ConexaoPostgresConfig MapearParaConfig(ConexaoPostgresRequest request) => new()
        {
            Host = request.Host.Trim(),
            Porta = request.Porta,
            Database = request.Database.Trim(),
            Usuario = request.Usuario.Trim(),
            Senha = request.Senha
        };

        private static ConexaoPostgresResponse MapearParaResponse(ConexaoPostgresConfig config) => new()
        {
            Host = config.Host,
            Porta = config.Porta,
            Database = config.Database,
            Usuario = config.Usuario,
            SenhaDefinida = !string.IsNullOrEmpty(config.Senha),
            EstaCompleta = config.EstaCompleta,
            DataAtualizacao = config.DataAtualizacao
        };

        private static ConexaoMongoConfig MapearParaConfig(ConexaoMongoRequest request) => new()
        {
            Host = request.Host.Trim(),
            Porta = request.Porta,
            Database = request.Database.Trim(),
            Usuario = request.Usuario.Trim(),
            Senha = request.Senha,
            AuthSource = string.IsNullOrWhiteSpace(request.AuthSource) ? "admin" : request.AuthSource.Trim()
        };

        private static ConexaoMongoResponse MapearParaResponse(ConexaoMongoConfig config) => new()
        {
            Host = config.Host,
            Porta = config.Porta,
            Database = config.Database,
            Usuario = config.Usuario,
            AuthSource = config.AuthSource,
            SenhaDefinida = !string.IsNullOrEmpty(config.Senha),
            EstaCompleta = config.EstaCompleta,
            DataAtualizacao = config.DataAtualizacao
        };
    }
}
