using ConexaoDinamica.Application.AplicationInterfaces.Autenticacao;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.ClienteRepositorios;
using ConexaoDinamica.Application.AplicationInterfaces.Auditoria;
using ConexaoDinamica.Application.AplicationInterfaces.Configuracoes;
using ConexaoDinamica.Application.AplicationInterfaces.Repositorios.UsuarioRepositorios;
using ConexaoDinamica.Application.Configuracoes;
using ConexaoDinamica.Application.Servicos.AutenticacaoServico;
using ConexaoDinamica.Infrastructure.Auditoria;
using ConexaoDinamica.Infrastructure.AuthService;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using ConexaoDinamica.Infrastructure.Data.Configuracoes;
using ConexaoDinamica.Infrastructure.Repositorios.ClienteRepositorio;
using ConexaoDinamica.Infrastructure.Repositorios.UsuarioRepositorio;
using LiteDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConexaoDinamica.Infrastructure.Data.DependencyInjections
{
    public static class InfrastructureExtension
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // DbContext — a connection string vem do store e é resolvida a cada
            // criação de contexto, permitindo trocar a conexão sem reiniciar.
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                var store = sp.GetRequiredService<IConexaoConfigStore>();
                var config = store.ObterPostgres();

                // Sem configuração, o contexto é criado assim mesmo e só falha ao
                // tentar abrir conexão. Lançar aqui derrubaria qualquer resolução
                // do DI que dependa do contexto, mesmo sem tocar no banco.
                string? connectionString = config is not null && config.EstaCompleta
                    ? MontadorConexaoPostgres.Montar(config)
                    : null;

                options.UseNpgsql(connectionString);

                // Resolvido do mesmo scope do contexto: o interceptor guarda estado
                // entre SavingChanges e SavedChanges, e precisa ser um por requisição.
                options.AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>());
            });

            // Store de configuração (LiteDB) — precisa existir mesmo sem Postgres configurado
            services.AddSingleton(_ => new LiteDatabase(MontarConnectionStringLiteDb(configuration)));
            services.AddSingleton<IConexaoConfigStore, LiteDbConexaoConfigStore>();

            // Casos de uso do AdminCenter
            services.AddScoped<IConexaoAdminService, ConexaoAdminService>();

            // Auditoria
            //
            // O provider é singleton porque o MongoClient mantém o próprio pool de
            // conexões e é projetado para viver durante toda a aplicação — criar um
            // por requisição vazaria pools. Ele se encarrega de recriar o client
            // quando a configuração muda.
            //
            // O interceptor é scoped: guarda os eventos coletados entre
            // SavingChanges e SavedChanges, e como singleton misturaria requisições
            // simultâneas.
            // Precisa acontecer antes da primeira gravação: o driver 3.x exige
            // representação explícita de Guid.
            MapeamentoAuditoriaMongo.Registrar();

            services.AddHttpContextAccessor();
            services.AddSingleton<IMongoConexaoProvider, MongoConexaoProvider>();
            services.AddScoped<IContextoAuditoria, ContextoAuditoria>();
            services.AddScoped<IAuditoriaRepository, MongoAuditoriaRepository>();
            services.AddScoped<IAuditoriaService, AuditoriaService>();
            services.AddScoped<AuditoriaSaveChangesInterceptor>();

            // Admin bootstrap (credenciais fora do banco)
            services.Configure<AdminBootstrapOptions>(
                configuration.GetSection(AdminBootstrapOptions.SectionName));

            // Services
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, UsuarioAuthService>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();

            // Repositories
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IClienteRepository, ClienteRepository>();

            return services;
        }

        /// <summary>
        /// Caminho do arquivo de configuração. Fora de bin/ de propósito: ali um
        /// rebuild apagaria a configuração junto com os binários.
        /// </summary>
        private static string MontarConnectionStringLiteDb(IConfiguration configuration)
        {
            var caminho = configuration["Storage:ConfigDbPath"];

            if (string.IsNullOrWhiteSpace(caminho))
            {
                var pasta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ConexaoDinamica");

                caminho = Path.Combine(pasta, "config.db");
            }

            var diretorio = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrWhiteSpace(diretorio))
                Directory.CreateDirectory(diretorio);

            return $"Filename={caminho};Connection=direct";
        }
    }
}
