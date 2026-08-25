using ConexaoDinamica.Domain.Entidades.Clientes;
using ConexaoDinamica.Domain.Entidades.Pedidos;
using ConexaoDinamica.Domain.Entidades.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace ConexaoDinamica.Infrastructure.Data.AppDBsContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }

        /// <summary>
        /// ItensPedido tem DbSet apenas por conveniência de consulta. Como parte do
        /// agregado, a manipulação normal acontece através de Pedido.Itens.
        /// </summary>
        public DbSet<ItemPedido> ItensPedido { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
