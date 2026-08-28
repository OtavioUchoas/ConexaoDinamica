using ConexaoDinamica.Infrastructure.Auditoria;
using ConexaoDinamica.Infrastructure.Data.AppDBsContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConexaoDinamica.Tests.Auditoria
{
    /// <summary>
    /// Banco descartável com o interceptor de auditoria ligado.
    ///
    /// ── Por que SQLite e não o provider InMemory ──────────────────────────────
    /// O InMemory não é um banco relacional: ele atribui as chaves no Add, e o
    /// ponto mais delicado do interceptor — a chave temporária negativa que só
    /// vira id real depois do INSERT — simplesmente não aconteceria. Os testes
    /// passariam sem exercitar a armadilha que a classe existe para contornar.
    ///
    /// O SQLite em memória gera a chave no INSERT, como o Postgres, e é o
    /// suficiente para reproduzir as duas fases de verdade. Custa uma conexão
    /// aberta: o banco vive enquanto ela viver, e fechá-la apaga tudo.
    /// </summary>
    internal sealed class AmbienteAuditoria : IDisposable
    {
        private readonly SqliteConnection _conexao;

        public RepositorioAuditoriaFalso Repositorio { get; } = new();

        public ContextoAuditoriaFalso Contexto { get; } = new();

        public AmbienteAuditoria()
        {
            _conexao = new SqliteConnection("DataSource=:memory:");
            _conexao.Open();

            using var criacao = NovoContexto();
            criacao.Database.EnsureCreated();
        }

        /// <summary>
        /// Um contexto novo, com um interceptor novo.
        ///
        /// O interceptor é scoped em produção justamente por guardar estado entre
        /// as duas fases; criar um por contexto reproduz esse ciclo de vida e
        /// impede que sobra de um teste vaze para o passo seguinte.
        /// </summary>
        public AppDbContext NovoContexto()
        {
            var interceptor = new AuditoriaSaveChangesInterceptor(
                Repositorio,
                Contexto,
                NullLogger<AuditoriaSaveChangesInterceptor>.Instance);

            var opcoes = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_conexao)
                .AddInterceptors(interceptor)
                .Options;

            return new ContextoDeTeste(opcoes);
        }

        public void Dispose() => _conexao.Dispose();

        /// <summary>
        /// O mapeamento real, menos o que é específico do Postgres.
        ///
        /// As entidades declaram DataCriacao/DataCadastro com
        /// HasDefaultValueSql("NOW()"), e o SQLite recusa a criação da tabela:
        /// para ele, DEFAULT precisa ser constante e NOW() não existe. Remover
        /// apenas os defaults preserva tudo que os testes precisam observar —
        /// chaves, relacionamentos, conversões — sem duplicar o mapeamento.
        ///
        /// Em troca, os testes precisam preencher as datas explicitamente, o que
        /// eles fariam de qualquer forma para ter valores previsíveis.
        /// </summary>
        private sealed class ContextoDeTeste : AppDbContext
        {
            public ContextoDeTeste(DbContextOptions<AppDbContext> opcoes)
                : base(opcoes)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                foreach (var propriedade in modelBuilder.Model
                             .GetEntityTypes()
                             .SelectMany(tipo => tipo.GetProperties()))
                {
                    propriedade.SetDefaultValueSql(null);
                }
            }
        }
    }
}
