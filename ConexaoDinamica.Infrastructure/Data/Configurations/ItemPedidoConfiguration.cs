using ConexaoDinamica.Domain.Entidades.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConexaoDinamica.Infrastructure.Data.Configurations
{
    public class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
    {
        public void Configure(EntityTypeBuilder<ItemPedido> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Quantidade).IsRequired();
            builder.Property(x => x.PrecoUnitario).IsRequired().HasPrecision(18, 2);

            builder.ToTable("ItensPedido");
        }
    }
}
