# Spec: Configurar ErrorHandlerMiddleware

## Contratos

---

### SC-EH-01 — Constructor clásico con LoggerWrapper, RequestDelegate e IStringLocalizerFactory

**Justificación**: `ErrorHandlerMiddleware` requiere tres dependencias que no admiten el constructor principal de C# moderno: `RequestDelegate` (parámetro especial de ASP.NET, no registrado en DI) y `IStringLocalizerFactory` (para obtener el localizer del tipo `SharedResources`). El constructor clásico es obligatorio aquí; el campo `_logger` tampoco puede ser de tipo `ILogger<T>`.

**Correcto**:
```csharp
public ErrorHandlerMiddleware(
    LoggerWrapper<ErrorHandlerMiddleware> logger,
    RequestDelegate next,
    IStringLocalizerFactory factory)
{
    _logger = logger;
    _next = next;
    _localizer = factory.Create(typeof(SharedResources));
}
```

**Incorrecto**:
```csharp
// Constructor principal — RequestDelegate no es inyectable así
public class ErrorHandlerMiddleware(
    ILogger<ErrorHandlerMiddleware> logger,   // incorrecto: ILogger en vez de LoggerWrapper
    RequestDelegate next,
    IStringLocalizer<SharedResources> localizer)  // incorrecto: no usa factory
{ }
```

---

### SC-EH-02 — Catch específico para "response has already started" con LogDebug

**Justificación**: Cuando ASP.NET Core ya inició la escritura de la respuesta, cualquier intento de modificarla lanza una `InvalidOperationException`. Si este caso llega al handler general, se intenta escribir una segunda respuesta y el servidor falla con un error de infraestructura. El catch de guarda con `LogDebug` lo descarta silenciosamente.

**Correcto**:
```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("response has already started"))
{
    _logger.LogDebug("Response has already started, ignoring exception: {Message}", ex.Message);
}
catch (Exception ex)
{
    await HandleException(context, ex);
}
```

**Incorrecto**:
```csharp
// Sin catch de guarda — el handler intentará escribir sobre una respuesta ya iniciada
catch (Exception ex)
{
    await HandleException(context, ex);
}
```

---

### SC-EH-03 — TUnauthorizedAccessException(InsufficientPermissions) antes del case general

**Justificación**: C# evalúa los casos del `switch` en orden. `TUnauthorizedAccessException` es una sola clase para 401 y 403. La distinción se hace por código. Si el case con `when` va después del case general, nunca se alcanzará porque el general lo atrapa primero.

**Correcto**:
```csharp
case TUnauthorizedAccessException ex
    when ex.Code == TUnauthorizedAccessException.InsufficientPermissions("").Code:
    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;    // 403
    break;
case TUnauthorizedAccessException ex:
    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized; // 401
    break;
```

**Incorrecto**:
```csharp
case TUnauthorizedAccessException ex:
    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized; // siempre 401
    break;
case TUnauthorizedAccessException ex                                 // nunca se alcanza
    when ex.Code == TUnauthorizedAccessException.InsufficientPermissions("").Code:
    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
    break;
```

---

### SC-EH-04 — Localización de mensajes vía SharedResources y reemplazos de placeholders

**Justificación**: Los mensajes de error son datos de presentación que deben ser localizables. `SharedResources` centraliza todos los textos; los placeholders `{Clave}` permiten mensajes contextuales sin concatenación de strings. El localizer usa el código de excepción como clave.

**Correcto**:
```csharp
if (exception is TException customException)
{
    var template = _localizer[customException.Code];
    string detail = template.Value;
    if (customException.Args != null)
        foreach (var kv in customException.Args)
            detail = detail.Replace($"{{{kv.Key}}}", kv.Value?.ToString());
    error.Message = detail;
}
```

```xml
<!-- SharedResources.resx -->
<data name="CANAL_OP_001">
  <value>El canal {Nombre} ya existe.</value>
</data>
```

**Incorrecto**:
```csharp
// Usar el mensaje de la excepción directamente — no localizable, expone detalles internos
error.Message = exception.Message;
```

---

### SC-EH-05 — Prefijo "WS:" en el mensaje de respuesta

**Justificación**: El prefijo `"WS: "` identifica que el mensaje proviene del Web Service, diferenciándolo de mensajes de otras capas (gateways, proxies, frontends). Facilita el triage en logs del lado del cliente cuando múltiples servicios están en juego.

**Correcto**:
```csharp
error.Message = $"WS: {error.Message}";
```

**Incorrecto**:
```csharp
error.Message = error.Message;  // sin prefijo
```

---

### SC-EH-06 — Serialización con CamelCase y WriteIndented

**Justificación**: La API utiliza `camelCase` consistentemente en todos sus endpoints. `WriteIndented = true` facilita la depuración en herramientas como Swagger y Postman. Las opciones se declaran como campo readonly para no reconstruirlas en cada request.

**Correcto**:
```csharp
private readonly JsonSerializerOptions _jsonSerializerOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

// En HandleException:
return context.Response.WriteAsync(
    JsonSerializer.Serialize(response, _jsonSerializerOptions));
```

**Incorrecto**:
```csharp
// Sin opciones de serialización — respuesta en PascalCase
return context.Response.WriteAsJsonAsync(response);
```

---

### SC-EH-07 — LogWarning para excepciones tipadas, LogError para no controladas

**Justificación**: Las excepciones de dominio (`TInvalidOperationException`, `TNotFoundException`, etc.) son fallos esperados del flujo de negocio. Loggearlos como `Error` activaría alertas innecesarias en los dashboards. `LogError` se reserva para situaciones realmente inesperadas que requieren investigación.

**Correcto**:
```csharp
case TNotFoundException ex:
    _logger.LogWarning(ex, "No se encontró el recurso [Codigo: {Codigo}]: {Message}.", ex.Code, ex.Message);
    break;
default:
    _logger.LogError(exception, "Error no controlado: {Message}.", exception.Message);
    break;
```

**Incorrecto**:
```csharp
case TNotFoundException ex:
    _logger.LogError(ex, "No encontrado: {Message}", ex.Message);  // LogError para algo esperado
    break;
```

---

### SC-EH-08 — UseMiddleware<ErrorHandlerMiddleware> al final del pipeline

**Justificación**: El middleware de errores debe envolver todo el pipeline para capturar excepciones de cualquier middleware anterior. Si va al inicio, no captura errores de autenticación, autorización ni localización. Si va antes de `UseRequestLocalization`, los mensajes localizados no se resuelven correctamente.

**Correcto**:
```csharp
app.UseCors(ALLOW_ALL_CORS_POLICY)
   .UseRateLimiter()
   .UseAuthentication()
   .UseAuthorization()
   .UseRequestLocalization(localizationOptions)
   .UseMiddleware<ErrorHandlerMiddleware>();    // último
```

**Incorrecto**:
```csharp
app.UseMiddleware<ErrorHandlerMiddleware>()    // primero — no captura errores de auth
   .UseCors(ALLOW_ALL_CORS_POLICY)
   .UseAuthentication()
   ...
```

---

### SC-EH-09 — ExceptionResponse con ErrorObjectResponse anidado

**Justificación**: El formato de respuesta de error es un contrato público de la API. Separar el envelope (`ExceptionResponse`) del detalle del error (`ErrorObjectResponse`) permite que los clientes traten ambos niveles independientemente y que el envelope sea extensible sin cambiar el contrato del error.

**Correcto**:
```json
{
  "statusCode": 400,
  "isError": true,
  "error": {
    "code": "CANAL_OP_001",
    "message": "WS: El canal Ventas ya existe.",
    "number": "",
    "data": ["Ventas"]
  }
}
```

**Incorrecto**:
```json
{
  "status": 400,
  "message": "El canal ya existe"
}
```
