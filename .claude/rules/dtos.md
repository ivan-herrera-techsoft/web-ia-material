---
description: Convenciones para DTOs (Request/Response)
globs: "**/Dtos/**"
---

## Estructura de carpetas

```
Application/Dtos/
└── Canales/
    ├── CrearCanalRequest.cs
    ├── ActualizarCanalRequest.cs
    ├── ObtenerCanalesResponse.cs
    └── ObtenerDetalleCanalResponse.cs

Api/Dtos/
├── Configurations/
│   ├── GeneralConfiguration.cs
│   ├── AccessTokensConfigurations.cs
│   ├── ApiCacheConfiguration.cs
│   ├── CorsConfiguration.cs
│   └── TelemetryConfiguration.cs
└── Pagination/
    ├── PaginationRequest.cs   ← query params + BindAsync
    └── PagedList.cs           ← List<T> + metadatos + ToPagedList()
```

## Naming

- Request: `{Operacion}{Entidad}Request` en español — ej. `CrearCanalRequest`, `ActualizarCanalRequest`
- Response: `Obtener{Entidad}Response` o `Obtener{Entidad}sResponse` — ej. `ObtenerCanalesResponse`
- Configuracion: `Configuracion{Feature}` — ej. `ConfiguracionJwt`, `ConfiguracionCache`

## Preferir records para DTOs inmutables

Usar `record` cuando el DTO no requiera herencia ni implementacion de interfaz.
Al **revisar** codigo existente con clases, es una preferencia, no una restriccion.

```csharp
// Requests — records inmutables
public record CrearCanalRequest(string Nombre, string? Descripcion);
public record ActualizarCanalRequest(string Nombre, string? Descripcion);

// Responses — records inmutables
public record ObtenerCanalesResponse(string Id, string Nombre, bool Activo);
public record ObtenerDetalleCanalResponse(
    string Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    DateTime CreadoEnUtc);
```

## PaginationRequest y PagedList

Los DTOs de paginación viven en `Api/Dtos/Pagination/` y son compartidos por todos los endpoints que devuelven listados. Ver → [Rules: pagination.md](pagination.md).

## Configuraciones (IOptions<T>)

Las clases de configuracion van en `Api/Dtos/Configurations/` y se registran con `Configure<T>()`:

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
```

## Reglas

- DTOs solo en la capa de Application (negocios) o `Api/Dtos/Configurations/` (configuracion)
- Un archivo por DTO
- DTOs de negocio en subcarpetas por entidad: `Dtos/{Entidad}/`
- Usar Mapster para conversiones: `Adapt<T>()` y `ProjectToType<T>()`
- Nombres en **español** (excepto sufijos tecnicos como `Request`, `Response`, `Configuration`)
