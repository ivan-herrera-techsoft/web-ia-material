# Spec: Background Services

Contratos de implementación para Background Services temporizados con `Bisoft.AutomatedServices.Util`.

---

## SC-BS-01 — Herencia obligatoria de TimedBackgroundService

Un Background Service **debe** heredar de `TimedBackgroundService` del paquete
`Bisoft.AutomatedServices.Util`. No se hereda de `BackgroundService` ni de
`IHostedService` directamente.

**Correcto**
```csharp
public class LimpiadorAccesosBackgroundService(...)
    : TimedBackgroundService(serviceProvider, logger)
{
    protected override ITimedServiceConfiguration Configuration => opciones.Value;
    protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken) { ... }
}
```

**Incorrecto**
```csharp
// ❌ Hereda de BackgroundService directamente — pierde el mecanismo de cron
public class LimpiadorAccesosBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) { ... }
}
```

> `TimedBackgroundService` gestiona el ciclo de ejecución según el cron configurado,
> crea el `IServiceScope` y llama a `ExecuteAutomatedTask` en cada disparo.

---

## SC-BS-02 — Servicios Scoped obtenidos del IServiceScope

Los servicios Scoped (repositorios, Application Services) **deben** obtenerse del
parámetro `IServiceScope scope` dentro de `ExecuteAutomatedTask`. **Nunca** se inyectan
en el constructor.

**Correcto**
```csharp
protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken)
{
    var repositorio = scope.ServiceProvider.GetRequiredService<IRepositorioSeguridad>();
    await repositorio.LimpiarSesionesExpiradas(stoppingToken);
}
```

**Incorrecto**
```csharp
// ❌ Repositorio Scoped inyectado en constructor de un Singleton
public class LimpiadorAccesosBackgroundService(
    IRepositorioSeguridad repositorio,  // ← PROHIBIDO: Scoped en Singleton
    ...)
    : TimedBackgroundService(...)
```

> Los Background Services son Singleton. Inyectar un servicio Scoped directamente
> provoca que el mismo scope se reutilice indefinidamente (captive dependency).

---

## SC-BS-03 — Configuración vía IOptions\<T\>

La configuración del servicio **debe** estar encapsulada en una clase que implemente
`ITimedServiceConfiguration` e inyectarse vía `IOptions<T>`. No se inyectan valores
escalares ni POCOs directamente.

**Correcto**
```csharp
public class ConfiguracionLimpiadorAccesos : ITimedServiceConfiguration
{
    public bool TimedServiceEnabled { get; init; }
    public string TimedServiceSchedule { get; init; } = null!;
}

public class LimpiadorAccesosBackgroundService(
    IOptions<ConfiguracionLimpiadorAccesos> opciones, ...)
    : TimedBackgroundService(...)
{
    protected override ITimedServiceConfiguration Configuration => opciones.Value;
}
```

**Incorrecto**
```csharp
// ❌ Parámetros de configuración inyectados como escalares
public class LimpiadorAccesosBackgroundService(
    bool habilitado,          // ← PROHIBIDO
    string cronExpresion,     // ← PROHIBIDO
    ...)
```

---

## SC-BS-04 — Registro condicional basado en TimedServiceEnabled

El servicio **debe** registrarse como `IHostedService` únicamente si `TimedServiceEnabled`
es `true` en la configuración. Un servicio desactivado no debe instanciarse.

**Correcto**
```csharp
// ServiceExtensions.cs
if (configuration.GetValue<bool>("AutomatedServices:LimpiadorAccesos:TimedServiceEnabled"))
    services.AddHostedService<LimpiadorAccesosBackgroundService>();
```

**Incorrecto**
```csharp
// ❌ Registro incondicional — el servicio se ejecuta siempre aunque esté "desactivado"
services.AddHostedService<LimpiadorAccesosBackgroundService>();
```

---

## SC-BS-05 — Configure\<\> en OptionsExtensions, AddHostedService\<\> en ServiceExtensions

El registro de la sección de configuración y el registro del servicio van en archivos
distintos. No se mezclan en el mismo extension method.

**Correcto**
```csharp
// Api/Extensions/Configuration/OptionsExtensions.cs
services.Configure<ConfiguracionLimpiadorAccesos>(
    configuration.GetSection("AutomatedServices:LimpiadorAccesos"));

// Api/Extensions/ServiceExtensions.cs
if (configuration.GetValue<bool>("AutomatedServices:LimpiadorAccesos:TimedServiceEnabled"))
    services.AddHostedService<LimpiadorAccesosBackgroundService>();
```

**Incorrecto**
```csharp
// ❌ Ambos registros mezclados en el mismo método
static IServiceCollection ConfigureBackgroundServices(this IServiceCollection services, ...)
{
    services.Configure<ConfiguracionLimpiadorAccesos>(...);       // ← en OptionsExtensions
    services.AddHostedService<LimpiadorAccesosBackgroundService>(); // ← en ServiceExtensions
}
```

---

## SC-BS-06 — Log al inicio y al fin, con captura de errores

`ExecuteAutomatedTask` **debe** registrar `LogInformation` al inicio y al final de cada
ejecución. Las excepciones **deben** capturarse y registrarse con `LogError` para evitar
que el error suprima el log de finalización.

**Correcto**
```csharp
protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken)
{
    logger.LogInformation("Iniciando tarea {Tarea}", nameof(LimpiadorAccesosBackgroundService));
    try
    {
        var eliminados = await repositorio.LimpiarSesionesExpiradas(stoppingToken);
        logger.LogInformation("Tarea {Tarea} completada: {Cantidad} registros",
            nameof(LimpiadorAccesosBackgroundService), eliminados);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error en tarea {Tarea}", nameof(LimpiadorAccesosBackgroundService));
    }
}
```

**Incorrecto**
```csharp
// ❌ Sin log de inicio/fin y sin manejo de errores
protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken)
{
    var repositorio = scope.ServiceProvider.GetRequiredService<IRepositorioSeguridad>();
    await repositorio.LimpiarSesionesExpiradas(stoppingToken);
}
```

---

## SC-BS-07 — CancellationToken propagado desde stoppingToken

El `CancellationToken` recibido en `ExecuteAutomatedTask` **debe** propagarse a todos los
métodos async que lo soporten. No se crea un `CancellationToken.None` dentro del task.

**Correcto**
```csharp
protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken)
{
    var resultado = await servicio.ProcesarPendientes(stoppingToken);
}
```

**Incorrecto**
```csharp
// ❌ CancellationToken ignorado — el task no puede cancelarse limpiamente
protected override async Task ExecuteAutomatedTask(IServiceScope scope, CancellationToken stoppingToken)
{
    var resultado = await servicio.ProcesarPendientes(CancellationToken.None);
}
```

---

## SC-BS-09 — ConfigurationReader valida en startup con AddSingleton + Options.Create

La configuración de un Background Service **debe** registrarse usando un `ConfigurationReader`
que valide los valores al arranque. Se usa `AddSingleton(Options.Create(...))`, no
`Configure<>` con `GetSection`.

**Correcto**
```csharp
// {Feature}ConfigurationsReader.cs — lee y valida
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

// OptionsExtensions.cs — registra con validación ya aplicada
services.AddSingleton(Options.Create(configuration.GetLimpiadorAccesosConfiguration()));
```

**Incorrecto**
```csharp
// ❌ GetSection no valida — un schedule vacío pasa silenciosamente al primer disparo
services.Configure<ConfiguracionLimpiadorAccesos>(
    configuration.GetSection("AutomatedServices:LimpiadorAccesos"));
```

> Con `GetSection`, un `TimedServiceSchedule` vacío o ausente no se detecta hasta el primer
> disparo del cron, lo que puede causar errores en producción. Con `Options.Create` + reader,
> la excepción se lanza en startup y el servicio no llega a arrancar.

---

## SC-BS-08 — Naming en español, sufijo BackgroundService

El nombre del feature va en **español**. El sufijo técnico `BackgroundService` queda en
inglés. La clase de configuración sigue el patrón `Configuracion{Feature}`.

**Correcto**
```
LimpiadorAccesosBackgroundService
SincronizadorCanalesBackgroundService
DepuradorEventosBackgroundService
ConfiguracionLimpiadorAccesos
ConfiguracionSincronizadorCanales
```

**Incorrecto**
```
// ❌ Feature en inglés
AccessCleanerBackgroundService
ChannelSynchronizerBackgroundService
// ❌ Sin sufijo
LimpiadorAccesos
// ❌ Sufijo en español
LimpiadorAccesosServicioFondo
```
