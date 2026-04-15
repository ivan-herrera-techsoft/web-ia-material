using Microsoft.EntityFrameworkCore;
using Techsoft.Sima.Web.Shared.Logging;

namespace Techsoft.Sima.Web.Infrastructure.Base;

public abstract class EFRepositoryBase<TContext, TRepository>
    where TContext : DbContext
{
    protected readonly TContext _context;
    protected readonly LoggerWrapper<TRepository> _logger;

    protected EFRepositoryBase(TContext context, LoggerWrapper<TRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public virtual async Task SaveChanges(
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Guardando cambios en base de datos");
        await _context.SaveChangesAsync(ct);
    }
}
