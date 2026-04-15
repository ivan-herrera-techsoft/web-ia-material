# Spec: HomeMapping — página de inicio

Contratos de implementación para `HomeMapping.cs` en `Api/Extensions/Endpoints/`.

---

## SC-HOME-01 — Clase partial de WebApplicationExtensions en el namespace Endpoints

`HomeMapping.cs` **debe** declararse como `public static partial class WebApplicationExtensions`
en el namespace `...Api.Extensions.Endpoints`. No crea una clase propia ni usa el namespace
raíz de `Extensions`.

**Correcto**
```csharp
namespace Company.Product.Module.Api.Extensions.Endpoints;

public static partial class WebApplicationExtensions
{
    public static WebApplication MapHome(this WebApplication app) { ... }
}
```

**Incorrecto**
```csharp
// ❌ Clase propia, no partial de WebApplicationExtensions
namespace Company.Product.Module.Api.Extensions.Endpoints;

public static class HomeEndpoint
{
    public static WebApplication MapHome(this WebApplication app) { ... }
}

// ❌ Namespace incorrecto (raíz de Extensions, no sub-namespace Endpoints)
namespace Company.Product.Module.Api.Extensions;

public static partial class WebApplicationExtensions { ... }
```

> `HomeMapping.cs` y `HealthChecksMapping.cs` son partes del mismo `WebApplicationExtensions`
> dividido por archivos usando `partial`. Esto mantiene cada responsabilidad en su propio archivo.

---

## SC-HOME-02 — Dos sobrecargas públicas de MapHome

Se exponen **dos** sobrecargas públicas: una sin rate limiting y otra con `rateLimitingPolicy`.
Ambas retornan `WebApplication` para permitir encadenamiento fluido.

**Correcto**
```csharp
public static WebApplication MapHome(this WebApplication app)
{
    app.Home();
    return app;
}

public static WebApplication MapHome(this WebApplication app, string rateLimitingPolicy)
{
    app.Home().RequireRateLimiting(rateLimitingPolicy);
    return app;
}
```

**Incorrecto**
```csharp
// ❌ Solo una sobrecarga con policy opcional — obliga a pasar string vacío cuando no aplica
public static WebApplication MapHome(this WebApplication app, string? rateLimitingPolicy = null)
{
    var builder = app.Home();
    if (!string.IsNullOrEmpty(rateLimitingPolicy))
        builder.RequireRateLimiting(rateLimitingPolicy);
    return app;
}
```

---

## SC-HOME-03 — Helper privado Home() centraliza el registro

La lógica del `MapGet` vive en un método **privado** `Home(this WebApplication app)` que
devuelve `RouteHandlerBuilder`. Las sobrecargas públicas solo llaman a `Home()` y opcionalmente
encadenan `RequireRateLimiting`.

**Correcto**
```csharp
private static RouteHandlerBuilder Home(this WebApplication app)
{
    return app.MapGet("/", [AllowAnonymous] async (...) => { ... })
              .WithMetadata(new SwaggerIgnoreAttribute());
}
```

**Incorrecto**
```csharp
// ❌ Lógica duplicada en cada sobrecarga pública
public static WebApplication MapHome(this WebApplication app)
{
    app.MapGet("/", [AllowAnonymous] async (...) => { ... }).WithMetadata(...);
    return app;
}
public static WebApplication MapHome(this WebApplication app, string rateLimitingPolicy)
{
    app.MapGet("/", [AllowAnonymous] async (...) => { ... }).WithMetadata(...).RequireRateLimiting(rateLimitingPolicy);
    return app;
}
```

---

## SC-HOME-04 — [AllowAnonymous] en el lambda, SwaggerIgnoreAttribute en metadata

El atributo `[AllowAnonymous]` va en el lambda del `MapGet`. El endpoint **debe** excluirse
de Swagger con `.WithMetadata(new SwaggerIgnoreAttribute())`.

**Correcto**
```csharp
app.MapGet("/", [AllowAnonymous] async (HttpContext context, ...) => { ... })
   .WithMetadata(new SwaggerIgnoreAttribute());
```

**Incorrecto**
```csharp
// ❌ AllowAnonymous fluido en lugar de atributo en el lambda
app.MapGet("/", async (...) => { ... })
   .AllowAnonymous()
   .WithMetadata(new SwaggerIgnoreAttribute());

// ❌ Falta SwaggerIgnoreAttribute — la home aparece en la documentación OpenAPI
app.MapGet("/", [AllowAnonymous] async (...) => { ... });
```

---

## SC-HOME-05 — Results.Content con "text/html", no Results.Ok

El handler **debe** retornar `Results.Content(html, "text/html")`. No se usa `Results.Ok()`
porque el content-type correcto para una página HTML es `text/html`.

**Correcto**
```csharp
return Results.Content(html, "text/html");
```

**Incorrecto**
```csharp
// ❌ Results.Ok — el browser recibe application/json y no renderiza el HTML
return Results.Ok(html);

// ❌ Escribir directamente en el response sin retornar IResult
await context.Response.WriteAsync(html);
```

---

## SC-HOME-06 — Try/catch alrededor de CheckHealthAsync — nunca propagar la excepción

El bloque `await healthCheckService.CheckHealthAsync()` **debe** estar dentro de un `try/catch`.
Si falla, se logea con `LogError` y el HTML muestra un mensaje de error. El endpoint **siempre**
retorna `200` con la página, nunca un `500`.

**Correcto**
```csharp
try
{
    var report = await healthCheckService.CheckHealthAsync();
    // ... construir checksHtml
}
catch (Exception ex)
{
    logger.LogError(ex, "Error al consultar health details desde página de bienvenida");
    content = "<div style=\"color:red;\">No se pudieron obtener los detalles de health.</div>";
}
return Results.Content(html, "text/html");  // siempre se ejecuta
```

**Incorrecto**
```csharp
// ❌ Sin try/catch — una excepción de health checks retorna 500 desde la home
var report = await healthCheckService.CheckHealthAsync();
```

---

## SC-HOME-07 — HOME_STYLE como private const string con raw string literal

El CSS **debe** estar en una constante `private const string HOME_STYLE` usando raw string
literal (`"""`). No se referencia ningún archivo `.css` externo ni se usa un string interpolado.

**Correcto**
```csharp
private const string HOME_STYLE =
    """
        .health-item { ... }
        .health-icon { ... }
        .healthy     { background-color: #4caf50; }
        .unhealthy   { background-color: #e53935; }
        .warning     { background-color: #ffb300; }
    """;
```

**Incorrecto**
```csharp
// ❌ CSS externo — la página deja de ser autónoma y requiere archivos estáticos
<link rel="stylesheet" href="/home.css" />

// ❌ Inline como string normal sin raw literal — difícil de mantener
private const string HOME_STYLE = ".health-item { display: flex; }\n.healthy { ... }";
```

---

## SC-HOME-08 — APP_NAME actualizado en ApiConstants

El nombre del servicio que aparece en la página (`Welcome {APP_NAME} API`) viene de
`ApiConstants.APP_NAME`. Al crear un nuevo proyecto, este valor **debe** actualizarse.
No se hardcodea el nombre dentro del `HomeMapping`.

**Correcto**
```csharp
// ApiConstants.cs
public const string APP_NAME = "Inventario";

// HomeMapping.cs — usa la constante
var mainText = $"Welcome {ApiConstants.APP_NAME} API";
```

**Incorrecto**
```csharp
// ❌ Nombre hardcodeado en HomeMapping
var mainText = "Welcome Inventario API";
```

---

## SC-HOME-09 — Encadenado después de AddHealthChecks en Program.cs

`MapHome` se registra en `Program.cs` **encadenado** inmediatamente después de `AddHealthChecks`.
Retorna `WebApplication` para permitir este encadenamiento fluido.

**Correcto**
```csharp
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY)
   .MapHome(FIXED_RATE_LIMITING_POLICY);
```

**Incorrecto**
```csharp
// ❌ Registrado por separado, no encadenado
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY);
app.MapHome(FIXED_RATE_LIMITING_POLICY);

// ❌ Registrado antes de AddHealthChecks — la home podría ejecutarse antes de que los
//    health checks estén configurados
app.MapHome(FIXED_RATE_LIMITING_POLICY);
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY);
```
