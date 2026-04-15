---
description: Convenciones para configuracion con IOptions<T> y appsettings
globs: "**/Configurations/**,**/Extensions/Configuration/**"
---

## Patron IOptions<T>

Usar `IOptions<T>` para todas las configuraciones tipadas. **No** inyectar POCOs directamente como Singleton.

```csharp
// Registro en DI (Extensions/OptionsExtensions.cs)
public static IServiceCollection ConfigureOptions(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<ConfiguracionJwt>(configuration.GetSection("Jwt"));
    services.Configure<ConfiguracionCache>(configuration.GetSection("Cache"));
    services.Configure<ConfiguracionCors>(configuration.GetSection("Cors"));
    services.Configure<ConfiguracionTelemetria>(configuration.GetSection("Telemetry"));
    return services;
}
```

## Cuando usar cada variante

| Tipo                   | Ciclo de vida | Cuando usar                                          |
|------------------------|---------------|------------------------------------------------------|
| `IOptions<T>`          | Singleton     | Configuracion estatica, no cambia en ejecucion       |
| `IOptionsSnapshot<T>`  | Scoped        | Configuracion que puede cambiar; recarga por request  |
| `IOptionsMonitor<T>`   | Singleton     | Reaccionar a cambios en tiempo real (OnChange)       |

## Uso en servicios

```csharp
// Con constructor principal (preferido en clases nuevas)
public class TokenService(IOptions<ConfiguracionJwt> opcionesJwt)
{
    public string GenerarToken(Usuario usuario)
    {
        var cfg = opcionesJwt.Value;
        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg.Llave));
        var credenciales = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: cfg.Emisor,
            audience: cfg.Audiencia,
            expires: DateTime.UtcNow.Add(cfg.DuracionToken),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

## Clases de configuracion

Ubicacion: `Api/Dtos/Configurations/`. Nombres en **español** con sufijo `Configuration` en ingles.

```csharp
public class ConfiguracionJwt
{
    public required string Llave { get; set; }
    public required string Emisor { get; set; }
    public required string Audiencia { get; set; }
    public TimeSpan DuracionToken { get; set; }
    public TimeSpan DuracionRefreshToken { get; set; }
}

public class ConfiguracionCache
{
    public bool CacheHabilitado { get; set; }
    public TimeSpan DuracionDeslizante { get; set; }
    public TimeSpan DuracionAbsoluta { get; set; }
}

public class ConfiguracionCors
{
    public string[]? OrigenesPermitidos { get; set; }
}

public class ConfiguracionTelemetria
{
    public bool Habilitado { get; set; }
    public required string DestinoTrazas { get; set; }
    public required string DestinoLogs { get; set; }
    public LogEventLevel NivelMinimoLogs { get; set; }
}

public class ConfiguracionServicioTemporizadoBase : ITimedServiceConfiguration
{
    public bool TimedServiceEnabled { get; init; }
    public string TimedServiceSchedule { get; init; } = null!;
}
```

## appsettings.json de referencia

```json
{
  "DatabaseConnections": {
    "App": {
      "Provider": "SqlServer",
      "ConnectionString": "Server=...;Database=...;"
    }
  },
  "Jwt": {
    "Llave": "your-secret-key-at-least-256-bits",
    "Emisor": "https://miapp.com",
    "Audiencia": "https://miapp.com",
    "DuracionToken": "00:30:00",
    "DuracionRefreshToken": "7.00:00:00"
  },
  "Cache": {
    "CacheHabilitado": true,
    "DuracionDeslizante": "00:05:00",
    "DuracionAbsoluta": "00:30:00"
  },
  "Cors": {
    "OrigenesPermitidos": ["http://localhost:3000"]
  },
  "Logger": {
    "LogHttpRequests": false,
    "Sqlite": {
      "Path": "Logs\\Logs.db",
      "MinimumLevel": "Information"
    }
  },
  "Telemetry": {
    "Enabled": false,
    "TracesDestination": "http://localhost:4318/v1/traces",
    "LogsDestination": "http://localhost:3100",
    "LogsMinimumLevel": "Error"
  },
  "AutomatedServices": {
    "LimpiadorAccesos": {
      "TimedServiceEnabled": true,
      "TimedServiceSchedule": "0 0 * ? * *"
    }
  },
  "RateLimiterMaxCalls": 100,
  "SensitiveData": {}
}
```

## Jerarquia de configuracion (mayor prioridad al ultimo)

1. `appsettings.json` — valores por defecto
2. `appsettings.{Environment}.json` — especificos de entorno
3. `/config/appsettings.json` — volumen Docker montado
4. Variables de entorno — mayor prioridad

```csharp
// Program.cs — cargar archivo Docker antes de leer la configuracion
builder.Configuration.AddJsonFile("/config/appsettings.json", optional: true, reloadOnChange: true);
```

## Datos sensibles

Agregar nodo `SensitiveData` en appsettings para parametros que requieran encriptacion.
En desarrollo usar `dotnet user-secrets`. Nunca hardcodear secretos en el codigo fuente.

## Reglas

- Toda configuracion tipada usa `IOptions<T>`, `IOptionsSnapshot<T>` o `IOptionsMonitor<T>`
- No inyectar POCOs de configuracion directamente como Singleton
- Clases de configuracion en `Api/Dtos/Configurations/`
- Nombre en español + sufijo `Configuration` en ingles
- Usar `required` para propiedades obligatorias
- Variables de entorno en `UPPER_SNAKE_CASE`
