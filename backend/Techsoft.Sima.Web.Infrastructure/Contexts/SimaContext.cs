using Microsoft.EntityFrameworkCore;
using Techsoft.Sima.Web.Domain.Entities.SolicitudesAcceso;
using Techsoft.Sima.Web.Domain.Entities.Usuarios;
using Techsoft.Sima.Web.Infrastructure.Mapping.SolicitudesAcceso.SqlServer;
using Techsoft.Sima.Web.Infrastructure.Mapping.Usuarios.SqlServer;

namespace Techsoft.Sima.Web.Infrastructure.Contexts;

public class SimaContext(DbContextOptions<SimaContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<SolicitudAcceso> SolicitudesAcceso { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UsuarioSqlServerConfiguration());
        modelBuilder.ApplyConfiguration(new SolicitudAccesoSqlServerConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
