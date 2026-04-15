---
description: Convenciones para Background Services temporizados con Bisoft.AutomatedServices
globs: "**/BackgroundServices/**"
---

## Background Services

Usan el paquete `Bisoft.AutomatedServices.Util` con la clase base `TimedBackgroundService`.
Se ubican en `Api/BackgroundServices/`. Usan **constructor principal**. El nombre del método
ejecutado va en **español**.

```csharp
namespace Company.Product.Module.Api.BackgroundServices;

public class LimpiadorAccesosBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<ConfiguracionLimpiadorAccesos> opciones,
    ILogger<LimpiadorAccesosBackgroundService> logger)
    : TimedBackgroundService(serviceProvider, logger)
{
    protected override ITimedServiceConfiguration Configuration => opciones.Value;

    protected override async Task ExecuteAutomatedTask(
        IServiceScope scope,
        CancellationToken stoppingToken)
    {
        // Obtener servicios Scoped del scope — NUNCA del constructor
        var repositorio = scope.ServiceProvider.GetRequiredService<IRepositorioSeguridad>();

        logger.LogInformation("Iniciando tarea {Tarea}", nameof(LimpiadorAccesosBackgroundService));
        try
        {
            var eliminados = await repositorio.LimpiarSesionesExpiradas(stoppingToken);
            logger.LogInformation("Tarea {Tarea} completada: {Cantidad} registros procesados",
                nameof(LimpiadorAccesosBackgroundService), eliminados);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en tarea {Tarea}", nameof(LimpiadorAccesosBackgroundService));
        }
    }
}
```

## Configuración con IOptions\<T\>

La clase de configuración implementa `ITimedServiceConfiguration` y se ubica en
`Api/Dtos/Configurations/`:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

public class ConfiguracionLimpiadorAccesos : ITimedServiceConfiguration
{
    public bool TimedServiceEnabled { get; init; }
    public string TimedServiceSchedule { get; init; } = null!;
}
```

## ConfigurationReader — lectura y validación al arranque

El ConfigurationReader es una clase `partial` de `ConfigurationExtensions` que lee,
valida y devuelve la configuración tipada **antes** de que la app arranque. Si algo está
mal configurado, la excepción se lanza en startup, no en la primera ejecución del task.

Se ubica en `Api/Extensions/Configuration/{Feature}ConfigurationsReader.cs`:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

internal static partial class ConfigurationExtensions
{
    internal static ConfiguracionLimpiadorAccesos GetLimpiadorAccesosConfiguration(
        this IConfiguration configuration)
    {
        return new ConfiguracionLimpiadorAccesos
        {
            TimedServiceEnabled  = configuration.GetTimedServiceEnabled("LimpiadorAccesos"),
            TimedServiceSchedule = configuration.GetTimedServiceSchedule("LimpiadorAccesos")
        };
    }

    private static bool GetTimedServiceEnabled(this IConfiguration configuration, string feature)
        => configuration.GetValue<bool>($"AutomatedServices:{feature}:TimedServiceEnabled");

    private static string GetTimedServiceSchedule(this IConfiguration configuration, string feature)
    {
        var schedule = configuration[$"AutomatedServices:{feature}:TimedServiceSchedule"];
        if (string.IsNullOrWhiteSpace(schedule))
            throw TEnvironmentException.InvalidConfiguration(
                TEnvironmentException.Sources.APPSETTINGS,
                $"El cron del servicio {feature} no está configurado");
        return schedule;
    }
}
```

Los helpers privados `GetTimedServiceEnabled` / `GetTimedServiceSchedule` son reutilizables
por todos los readers del proyecto (uno por feature, misma clase partial).

## Registro DI

El registro se divide en dos archivos de extensión:

**`Api/Extensions/Configuration/OptionsExtensions.cs`** — registra la configuración
validada con `AddSingleton` + `Options.Create` (no `GetSection`):

```csharp
services.AddSingleton(Options.Create(configuration.GetLimpiadorAccesosConfiguration()));
```

**`Api/Extensions/ServiceExtensions.cs`** — registra el servicio de forma condicional,
leyendo el flag directamente de configuración (antes de que `IOptions<T>` esté disponible):

```csharp
if (configuration.GetValue<bool>("AutomatedServices:LimpiadorAccesos:TimedServiceEnabled"))
    services.AddHostedService<LimpiadorAccesosBackgroundService>();
```

> Usar `AddSingleton(Options.Create(...))` en lugar de `Configure<>` + `GetSection` garantiza
> que la validación ocurre en startup. Con `GetSection`, un schedule vacío o nulo no se detecta
> hasta el primer disparo del cron.
>
> El registro condicional evita que el servicio se instancie (ni siquiera como Singleton)
> cuando está desactivado en configuración.

## appsettings.json

```json
{
  "AutomatedServices": {
    "LimpiadorAccesos": {
      "TimedServiceEnabled": true,
      "TimedServiceSchedule": "0 0/30 * * * ?"
    }
  }
}
```

## Formato Cron (Quartz — 6 campos)

```
segundos  minutos  horas  dia-mes  mes  dia-semana
0 0/30 * * * ?      → Cada 30 minutos
0 0 * ? * *         → Cada hora
0 0 3 * * ?         → Cada día a las 3:00 AM
0 0 0 ? * MON       → Cada lunes a medianoche
0 0 6 ? * MON-FRI   → Lunes a viernes a las 6:00 AM
```

- `*` = cualquier valor
- `?` = sin valor específico (solo en dia-mes o dia-semana)
- `0/30` = cada 30 unidades a partir de 0
- `MON-FRI` = rango de días

## Manejo de errores

`ExecuteAutomatedTask` debe capturar y loguear excepciones internamente. Si una excepción
no se captura, la clase base `TimedBackgroundService` la registra pero el servicio continúa
ejecutándose en el siguiente ciclo. Capturar explícitamente permite un log más descriptivo.

## Reglas críticas

- Background Services son **Singleton** por naturaleza de `IHostedService`
- **NUNCA** inyectar servicios Scoped directamente en el constructor — obtenerse siempre
  del `IServiceScope scope` dentro de `ExecuteAutomatedTask`
- Configuración vía `IOptions<T>` (nunca POCO inyectado directamente)
- `LogInformation` al inicio y al final de cada ejecución
- Capturar excepciones dentro del task y loguear con `LogError`
- Registro condicional en `ServiceExtensions` basado en `TimedServiceEnabled`
- `Configure<>` va en `OptionsExtensions`, `AddHostedService<>` va en `ServiceExtensions`

## Naming

| Elemento             | Patrón                        | Ejemplo                              |
|----------------------|-------------------------------|--------------------------------------|
| Clase                | `{Feature}BackgroundService`  | `LimpiadorAccesosBackgroundService`  |
| Configuración        | `Configuracion{Feature}`      | `ConfiguracionLimpiadorAccesos`      |
| Sección appsettings  | `AutomatedServices:{Feature}` | `AutomatedServices:LimpiadorAccesos` |

El feature **siempre en español**: `LimpiadorAccesos`, `SincronizadorCanales`, `DepuradorEventos`.
