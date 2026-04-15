---
description: Convenciones para extension methods de DI y configuracion
globs: "**/Extensions/**"
---

## Extension methods para DI

Todo el registro de DI se hace via extension methods sobre `IServiceCollection`:

```csharp
public static class ServiceExtensions
{
    public static IServiceCollection ConfigureRepositories(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IRepositorio, Repository<AppContext>>();
        services.AddScoped<ICanalRepository, CanalRepository>();

        if (configuration.GetValue<bool>("Cache:CacheHabilitado"))
            services.AddScoped<IUsuarioRepository, UsuarioCachedRepository>();
        else
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        return services;
    }

    public static IServiceCollection ConfigureDomainServices(this IServiceCollection services)
    {
        services.AddScoped<CanalDomainService>();
        return services;
    }

    public static IServiceCollection ConfigureAppServices(this IServiceCollection services)
    {
        services.AddScoped<CanalService>();
        return services;
    }
}
```

## Configuracion con IOptions<T>

Registrar todas las configuraciones tipadas con `Configure<T>()`, **no** como singletons directos:

```csharp
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

## Archivo por extension

| Metodo                        | Archivo                                          |
|-------------------------------|--------------------------------------------------|
| `ConfigureOptions()`          | `Extensions/OptionsExtensions.cs`                |
| `ConfigureAuthentication()`   | `Extensions/AuthenticationExtensions.cs`         |
| `ConfigureApiVersioning()`    | `Extensions/VersioningExtensions.cs`             |
| `ConfigureSwagger()`          | `Extensions/SwaggerExtensions.cs`                |
| `ConfigureCors()`             | `Extensions/CorsExtensions.cs`                   |
| `ConfigureHealthChecks()`     | `Extensions/HealthChecksExtensions.cs`           |
| `ConfigureLogger()`           | `Extensions/LoggerConfigurationExtensions.cs`    |
| `ConfigureContexts/Repos/etc` | `Extensions/ServiceExtensions.cs`                |
| `ConfigureRateLimiter()`      | `Extensions/RateLimiterExtensions.cs`            |
| `ConfigureTelemetry()`        | `Extensions/TelemetryExtensions.cs`              |
| `MapEndpoints()`              | `Extensions/EndpointExtensions.cs`               |

## Override con variables de entorno

```csharp
public static string? SobreescribirConVariableDeEntorno(this string? valor, string nombreVariable)
{
    var valorEntorno = Environment.GetEnvironmentVariable(nombreVariable);
    return string.IsNullOrEmpty(valorEntorno) ? valor : valorEntorno;
}
```

## Jerarquia de configuracion

1. `appsettings.json` (valores por defecto)
2. `appsettings.{Environment}.json` (especificos de entorno)
3. `/config/appsettings.json` (volumen Docker montado)
4. Variables de entorno (mayor prioridad)

```csharp
// En Program.cs
builder.Configuration.AddJsonFile("/config/appsettings.json", optional: true, reloadOnChange: true);
```

## Reglas

- Retornar siempre `IServiceCollection` para encadenamiento fluido
- Nombre: `Configure{Feature}` para registros, `Get{Section}Configuration` para readers
- Registro condicional basado en configuracion (cache, background services)
- Orden de registro importa en Program.cs (autenticacion primero)
- Usar constructor principal en clase nueva; omitir al revisar codigo existente
