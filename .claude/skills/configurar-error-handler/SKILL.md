# Skill: Configurar ErrorHandlerMiddleware

Configura el middleware centralizado de manejo de errores con localización en un proyecto Atenea Minimal API. Requiere que `ConfigureLocalization` esté registrado.

---

## Paso 1 — DTOs de respuesta de error

Crear `Api/Dtos/BaseResponses/ErrorObjectResponse.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.BaseResponses;

public class ErrorObjectResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public IEnumerable<object> Data { get; set; } = [];
}
```

Crear `Api/Dtos/BaseResponses/ExceptionResponse.cs`:

```csharp
namespace Company.Product.Module.Api.Dtos.BaseResponses;

public class ExceptionResponse
{
    public int StatusCode { get; set; } = 500;
    public bool IsError { get; set; } = true;
    public required ErrorObjectResponse Error { get; set; }
}
```

---

## Paso 2 — SharedResources para localización

Crear clase marcadora `Api/Resources/SharedResources.cs`:

```csharp
namespace Company.Product.Module.Api.Resources;

public class SharedResources { }
```

Crear `Api/Resources/SharedResources.resx` con los códigos de error del dominio como claves:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="ErrorInterno">
    <value>El servicio no está disponible, comuníquese con soporte.</value>
  </data>
  <!-- Códigos de negocio del módulo -->
  <!-- Ejemplo: -->
  <!-- <data name="MODOP0001"><value>El canal {Nombre} ya existe.</value></data> -->
</root>
```

Agregar variantes por cultura si se requieren (`SharedResources.es-MX.resx`, `SharedResources.es-GT.resx`) con las mismas claves y valores localizados.

Los códigos de excepción deben estar en `DomainConstants.ExceptionCodes` y coincidir exactamente con las claves del resx.

---

## Paso 3 — ConfigureLocalization en ServiceExtensions

En `Api/Extensions/ServiceExtensions.cs` agregar:

```csharp
private static readonly string[] _supportedLanguages = ["es-MX", "es-GT"];

public static IServiceCollection ConfigureLocalization(this IServiceCollection services)
{
    services.AddLocalization();
    var supportedCultures = _supportedLanguages.Select(c => new CultureInfo(c)).ToList();
    services.Configure<RequestLocalizationOptions>(options =>
    {
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.RequestCultureProviders = new IRequestCultureProvider[]
        {
            new AcceptLanguageHeaderRequestCultureProvider()
        };
        options.SetDefaultCulture("es-MX");
    });
    return services;
}
```

Encadenar en `Program.cs`:

```csharp
builder.Services
    ...
    .ConfigureLocalization()
    ...
```

---

## Paso 4 — ErrorHandlerMiddleware

Crear `Api/Middlewares/ErrorHandlerMiddleware.cs`:

```csharp
using Bisoft.Exceptions;
using Bisoft.Logging.Util;
using Company.Product.Module.Api.Dtos.BaseResponses;
using Company.Product.Module.Api.Resources;
using System.Net;
using System.Text.Json;

namespace Company.Product.Module.Api.Middlewares;

public class ErrorHandlerMiddleware
{
    private readonly LoggerWrapper<ErrorHandlerMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly IStringLocalizer _localizer;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ErrorHandlerMiddleware(
        LoggerWrapper<ErrorHandlerMiddleware> logger,
        RequestDelegate next,
        IStringLocalizerFactory factory)
    {
        _logger = logger;
        _next = next;
        _localizer = factory.Create(typeof(SharedResources));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("response has already started"))
        {
            _logger.LogDebug("Response has already started, ignoring exception: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            await HandleException(context, ex);
        }
    }

    private Task HandleException(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var error = new ErrorObjectResponse();
        IEnumerable<object> additionalData = [];

        switch (exception)
        {
            case TInvalidOperationException ex:
                _logger.LogWarning(ex, "Operación no permitida [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            case TArgumentException ex:
                _logger.LogWarning(ex, "Error de argumento [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            case TNotFoundException ex:
                _logger.LogWarning(ex, "No se encontró el recurso [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;
            case TUnauthorizedAccessException ex
                when ex.Code == TUnauthorizedAccessException.InsufficientPermissions("").Code:
                _logger.LogWarning(ex, "Acceso no concedido [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                break;
            case TUnauthorizedAccessException ex:
                _logger.LogWarning(ex, "Acceso denegado [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;
            case BadHttpRequestException ex:
                _logger.LogWarning(ex, "Error de argumento [Codigo: {Codigo}]: {Message}.",
                    TArgumentException.InvalidFormat("").Code, ex.Message);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            default:
                _logger.LogError(exception, "Error no controlado: {Message}.", exception.Message);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        if (exception is TException customException)
        {
            error.Code = customException.Code;
            additionalData = customException.Args?.Values.ToList() ?? [];
            var template = _localizer[customException.Code];
            string detail = template.Value;
            if (customException.Args != null)
                foreach (var kv in customException.Args)
                    detail = detail.Replace($"{{{kv.Key}}}", kv.Value?.ToString());
            error.Message = detail;
        }
        else
        {
            error.Code = exception is BadHttpRequestException
                ? TArgumentException.InvalidFormat("").Code
                : "ErrorInterno";
            error.Message = _localizer[error.Code].Value;
        }

        error.Data = additionalData;
        error.Message = $"WS: {error.Message}";

        var response = new ExceptionResponse
        {
            StatusCode = context.Response.StatusCode,
            Error = error,
            IsError = true
        };
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, _jsonSerializerOptions));
    }
}
```

---

## Paso 5 — Registrar en el pipeline (Program.cs)

Verificar que el pipeline sigue el orden correcto. `UseMiddleware<ErrorHandlerMiddleware>()` va al final, después de `UseRequestLocalization`:

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   .UseRequestLocalization(
       app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value)
   .UseMiddleware<ErrorHandlerMiddleware>();
```

---

## Paso 6 — Agregar códigos de excepción a los resx

Por cada `TInvalidOperationException` o `TNotFoundException` que el dominio pueda lanzar, agregar su código como entrada en los archivos `.resx`:

```xml
<!-- Ejemplo para un módulo de Canales -->
<data name="CANAL_OP_001">
  <value>El canal {Nombre} ya existe en el sistema.</value>
</data>
<data name="CANAL_NF_001">
  <value>No se encontró el canal con id {CanalId}.</value>
</data>
```

Los códigos deben definirse en `DomainConstants.ExceptionCodes.Operation` y `ExceptionCodes.Argument`.

---

## Verificación

- [ ] `ErrorObjectResponse` y `ExceptionResponse` existen en `Dtos/BaseResponses/`
- [ ] `SharedResources.cs` es una clase vacía en `Resources/`
- [ ] `SharedResources.resx` tiene `ErrorInterno` y todos los códigos de dominio
- [ ] `ConfigureLocalization` registra `es-MX` como cultura por defecto
- [ ] `ErrorHandlerMiddleware` usa `LoggerWrapper<T>` — no `ILogger<T>`
- [ ] Usa constructor clásico (no principal) por la inyección de `RequestDelegate` y factory
- [ ] `case TUnauthorizedAccessException when ...InsufficientPermissions` va antes del case general
- [ ] `BadHttpRequestException` mapeado a 400 con código de `TArgumentException.InvalidFormat`
- [ ] Catch especial para `"response has already started"` con `LogDebug`
- [ ] Respuesta serializada con `CamelCase` y `WriteIndented = true`
- [ ] `Message` prefijado con `"WS: "`
- [ ] `UseMiddleware<ErrorHandlerMiddleware>()` va al final del pipeline
