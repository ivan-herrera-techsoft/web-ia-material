---
description: Convenciones para ErrorHandlerMiddleware — mapeo de excepciones Bisoft a HTTP y orden en el pipeline
globs: "**/Middleware/**,**/Extensions/**"
---

# Reglas de ErrorHandlerMiddleware

## Principio general

`ErrorHandlerMiddleware` es el único middleware de manejo de errores. Centraliza el mapeo de excepciones tipadas de Bisoft a respuestas HTTP, aplica localización de mensajes de error y registra logs por nivel de gravedad. Va al final del pipeline, después de `UseRequestLocalization`.

---

## DTOs de respuesta de error

```csharp
// Api/Dtos/BaseResponses/ErrorObjectResponse.cs
public class ErrorObjectResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public IEnumerable<object> Data { get; set; } = [];
}

// Api/Dtos/BaseResponses/ExceptionResponse.cs
public class ExceptionResponse
{
    public int StatusCode { get; set; } = 500;
    public bool IsError { get; set; } = true;
    public required ErrorObjectResponse Error { get; set; }
}
```

---

## ErrorHandlerMiddleware

```csharp
// Api/Middlewares/ErrorHandlerMiddleware.cs
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

        var response = new ExceptionResponse()
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

## Mapeo de excepciones a HTTP

| Excepción                                     | Status Code | Log Level    |
|-----------------------------------------------|-------------|--------------|
| `TInvalidOperationException`                  | 400         | LogWarning   |
| `TArgumentException`                          | 400         | LogWarning   |
| `BadHttpRequestException`                     | 400         | LogWarning   |
| `TNotFoundException`                          | 404         | LogWarning   |
| `TUnauthorizedAccessException` (InsufficientPermissions) | 403 | LogWarning |
| `TUnauthorizedAccessException`                | 401         | LogWarning   |
| `Exception` (cualquier otro)                  | 500         | LogError     |

---

## Localización de mensajes (SharedResources)

Los mensajes de error se resuelven desde archivos `.resx` usando el código de excepción como clave. El middleware los busca en `SharedResources` con soporte multi-idioma via `Accept-Language` header.

```csharp
// Api/Resources/SharedResources.cs — clase marcadora vacía
public class SharedResources { }
```

Archivos de recursos:
- `SharedResources.resx` — mensajes por defecto (español)
- `SharedResources.es-MX.resx` — variante México
- `SharedResources.es-GT.resx` — variante Guatemala

Formato de entradas en `.resx`:

```xml
<data name="ErrorInterno">
  <value>El servicio no está disponible, comuníquese con soporte.</value>
</data>
<data name="GENUA0001">
  <value>Credenciales incorrectas.</value>
</data>
<data name="GENOP0001">
  <value>El usuario {User} ya existe.</value>
</data>
```

Los placeholders `{Clave}` son reemplazados con los valores de `TException.Args`.

---

## Configuración de localización (ConfigureLocalization)

```csharp
// Api/Extensions/ServiceExtensions.cs
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

En `Program.cs`, activar antes de `UseMiddleware`:

```csharp
app.UseRequestLocalization(
    app.Services
       .GetRequiredService<IOptions<RequestLocalizationOptions>>()
       .Value)
   .UseMiddleware<ErrorHandlerMiddleware>();
```

---

## Orden del pipeline (Program.cs)

```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   .UseRequestLocalization(...)
   .UseMiddleware<ErrorHandlerMiddleware>();   // siempre el último
```

---

## Formato de respuesta serializado

```json
{
  "statusCode": 400,
  "isError": true,
  "error": {
    "code": "GENOP0001",
    "message": "WS: El usuario admin ya existe.",
    "number": "",
    "data": ["admin"]
  }
}
```

Características del formato:
- `camelCase` via `JsonNamingPolicy.CamelCase`
- `WriteIndented = true`
- `message` siempre prefijado con `"WS: "`
- `data` contiene los valores de `TException.Args` para contexto adicional
