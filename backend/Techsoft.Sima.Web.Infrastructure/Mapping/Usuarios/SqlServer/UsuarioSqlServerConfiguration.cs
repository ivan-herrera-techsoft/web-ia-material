using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Techsoft.Sima.Web.Domain.Entities.Usuarios;
using Techsoft.Sima.Web.Domain.Enums;
using static Techsoft.Sima.Web.Domain.DomainConstants;

namespace Techsoft.Sima.Web.Infrastructure.Mapping.Usuarios.SqlServer;

internal class UsuarioSqlServerConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios", "usr");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType("uniqueidentifier")
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Email)
            .HasColumnType("nvarchar")
            .HasMaxLength(Values.MAX_LENGTH_EMAIL)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(e => e.Nombre)
            .HasColumnType("nvarchar")
            .HasMaxLength(Values.MAX_LENGTH_NOMBRE)
            .HasColumnName("nombre")
            .IsRequired();

        builder.Property(e => e.ApellidoPaterno)
            .HasColumnType("nvarchar")
            .HasMaxLength(Values.MAX_LENGTH_APELLIDO)
            .HasColumnName("apellidoPaterno")
            .IsRequired();

        builder.Property(e => e.ApellidoMaterno)
            .HasColumnType("nvarchar")
            .HasMaxLength(Values.MAX_LENGTH_APELLIDO)
            .HasColumnName("apellidoMaterno");

        builder.Property(e => e.Rol)
            .HasColumnType("nvarchar")
            .HasMaxLength(Values.MAX_LENGTH_ROL)
            .HasColumnName("rol")
            .IsRequired();

        builder.Property(e => e.Estatus)
            .HasColumnType("int")
            .HasColumnName("estatus")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.FechaCreacionUtc)
            .HasColumnType("datetime2")
            .HasColumnName("fechaCreacionUtc")
            .IsRequired();

        builder.Property(e => e.FechaActualizacionUtc)
            .HasColumnType("datetime2")
            .HasColumnName("fechaActualizacionUtc")
            .IsRequired();

        builder.HasIndex(e => e.Email).IsUnique();
    }
}
