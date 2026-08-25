using ConexaoDinamica.Domain.Entidades.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConexaoDinamica.Infrastructure.Data.Configurations
{
    public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
    {
        public void Configure(EntityTypeBuilder<Pedido> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Numero).IsRequired().HasMaxLength(30);
            builder.HasIndex(x => x.Numero).IsUnique();

            // String em vez do int padrão: mesma escolha feita para PerfilUsuario.
            // Reordenar o enum reinterpretaria os registros existentes.
            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(x => x.Total).IsRequired().HasPrecision(18, 2);
            builder.Property(x => x.DataCriacao).IsRequired().HasDefaultValueSql("NOW()");

            // Referência a outro agregado: Restrict impede apagar um cliente que
            // ainda tenha pedidos, preservando a integridade do histórico.
            builder.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Partes do agregado: Cascade porque itens não existem sem o pedido.
            builder.HasMany(x => x.Itens)
                .WithOne(i => i.Pedido!)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.ToTable("Pedidos");
        }
    }
}
