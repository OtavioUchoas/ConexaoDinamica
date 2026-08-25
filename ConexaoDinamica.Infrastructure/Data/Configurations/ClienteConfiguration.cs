using ConexaoDinamica.Domain.Entidades.Clientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConexaoDinamica.Infrastructure.Data.Configurations
{
    public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
    {
        public void Configure(EntityTypeBuilder<Cliente> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Nome).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Documento).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Email).HasMaxLength(150);
            builder.Property(x => x.DataCadastro).IsRequired().HasDefaultValueSql("NOW()");

            builder.HasIndex(x => x.Documento).IsUnique();

            builder.ToTable("Clientes");
        }
    }
}
