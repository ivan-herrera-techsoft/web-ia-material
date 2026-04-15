using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Techsoft.Sima.Web.Api.Helpers.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy("Servicio de almacenamiento disponible.");
        }
        catch
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Servicio de almacenamiento no disponible.");
        }
    }
}
