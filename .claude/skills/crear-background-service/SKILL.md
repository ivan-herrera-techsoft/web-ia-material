---
name: crear-background-service
description: Crea un Background Service temporizado con Bisoft.AutomatedServices
argument-hint: <NombreFeature en español> ej. "LimpiadorAccesos", "SincronizadorCanales"
---

Crear un Background Service temporizado para **$ARGUMENTS**.

## 1. Clase de configuración

Crear `Api/Dtos/Configurations/Configuracion$ARGUMENTS.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.Configurations;

public class Configuracion$ARGUMENTS : ITimedServiceConfiguration
{
    public bool TimedServiceEnabled { get; init; }
    public string TimedServiceSchedule { get; init; } = null!;
}
```

## 2. Background Service

Crear `Api/BackgroundServices/$ARGUMENTSBackgroundService.cs`:

```csharp
namespace Company.Product.Module.Api.BackgroundServices;

public class $ARGUMENTSBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<Configuracion$ARGUMENTS> opciones,
    ILogger<$ARGUMENTSBackgroundService> logger)
    : TimedBackgroundService(serviceProvider, logger)
{
    protected override ITimedServiceConfiguration Configuration => opciones.Value;

    protected override async Task ExecuteAutomatedTask(
        IServiceScope scope,
        CancellationToken stoppingToken)
    {
        // Obtener servicios Scoped del scope — NUNCA del constructor
        var servicio = scope.ServiceProvider.GetRequiredService<I{Servicio}>();

        logger.LogInformation("Iniciando tarea {Tarea}", nameof($ARGUMENTSBackgroundService));
        try
        {
            // Lógica de la tarea
            logger.LogInformation("Tarea {Tarea} completada", nameof($ARGUMENTSBackgroundService));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en tarea {Tarea}", nameof($ARGUMENTSBackgroundService));
        }
    }
}
```

## 3. Sección en appsettings.json

Agregar dentro del nodo `AutomatedServices` (crear el nodo si no existe):

```json
{
  "AutomatedServices": {
    "$ARGUMENTS": {
      "TimedServiceEnabled": true,
      "TimedServiceSchedule": "0 0/30 * * * ?"
    }
  }
}
```

Formatos cron comunes (Quartz — 6 campos: segundos minutos horas dia-mes mes dia-semana):

| Expresión           | Significado                      |
|---------------------|----------------------------------|
| `0 0/30 * * * ?`    | Cada 30 minutos                  |
| `0 0 * ? * *`       | Cada hora                        |
| `0 0 3 * * ?`       | Cada día a las 3:00 AM           |
| `0 0 0 ? * MON`     | Cada lunes a medianoche          |
| `0 0 6 ? * MON-FRI` | Lunes a viernes a las 6:00 AM    |

## 4. ConfigurationReader

Crear `Api/Extensions/Configuration/$ARGUMENTSConfigurationsReader.cs`.
Si los helpers `GetTimedServiceEnabled` / `GetTimedServiceSchedule` ya existen en el proyecto
(como métodos privados de otra clase partial), solo agregar el método público:

```csharp
namespace Company.Product.Module.Api.Extensions.Configuration;

internal static partial class ConfigurationExtensions
{
    internal static Configuracion$ARGUMENTS Get$ARGUMENTSConfiguration(
        this IConfiguration configuration)
    {
        return new Configuracion$ARGUMENTS
        {
            TimedServiceEnabled  = configuration.GetTimedServiceEnabled("$ARGUMENTS"),
            TimedServiceSchedule = configuration.GetTimedServiceSchedule("$ARGUMENTS")
        };
    }

    // Agregar solo si no existen ya en otra clase partial del mismo namespace:
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

## 5. Registro en OptionsExtensions.cs

En `Api/Extensions/Configuration/OptionsExtensions.cs`, agregar usando el reader:

```csharp
services.AddSingleton(Options.Create(configuration.Get$ARGUMENTSConfiguration()));
```

> `AddSingleton(Options.Create(...))` en lugar de `Configure<>` + `GetSection` garantiza
> que la validación del schedule ocurre en startup, no en el primer disparo del cron.

## 6. Registro condicional en ServiceExtensions.cs

En `Api/Extensions/ServiceExtensions.cs`, agregar **condicionalmente**:

```csharp
if (configuration.GetValue<bool>("AutomatedServices:$ARGUMENTS:TimedServiceEnabled"))
    services.AddHostedService<$ARGUMENTSBackgroundService>();
```

> El registro condicional es obligatorio: cuando `TimedServiceEnabled = false` el servicio
> no se instancia en absoluto (no se ejecuta ni en segundo plano).

## Checklist

- [ ] `Configuracion$ARGUMENTS` implementa `ITimedServiceConfiguration`
- [ ] `$ARGUMENTSBackgroundService` hereda `TimedBackgroundService`
- [ ] Servicios Scoped obtenidos de `scope.ServiceProvider`, no del constructor
- [ ] `LogInformation` al inicio y al final del task
- [ ] Excepciones capturadas con `LogError` dentro del try/catch
- [ ] `$ARGUMENTSConfigurationsReader.cs` creado con `Get$ARGUMENTSConfiguration()`
- [ ] `AddSingleton(Options.Create(...))` en `OptionsExtensions.cs` (no `Configure<>` + `GetSection`)
- [ ] `AddHostedService<>` condicional en `ServiceExtensions.cs`
- [ ] Sección en `appsettings.json` con `TimedServiceEnabled` y `TimedServiceSchedule`
- [ ] Feature name en español
