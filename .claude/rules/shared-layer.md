---
description: Convenciones para la capa Shared — constantes, helpers y DTOs compartidos entre módulos en arquitectura Atenea
globs: "**/Shared/**"
---

## Propósito de la capa Shared

La capa `Shared/` contiene elementos que **más de un módulo necesita** pero que no pertenecen a ningún módulo de negocio específico. Evita duplicación entre módulos dentro del mismo proyecto.

---

## Qué va en Shared

| Elemento | Ejemplo | Notas |
|---|---|---|
| Constantes globales de proyecto | `ApiConstants.VERSION_1` | Usado por todos los endpoints |
| Constantes de políticas | `ALLOW_ALL_CORS_POLICY`, `FIXED_RATE_LIMITING_POLICY` | Usadas en Program.cs y endpoints |
| DTOs compartidos entre módulos | `SolicitudPaginacion`, `ListaPaginada<T>` | Paginación aplica a múltiples módulos |
| Helpers/extensiones transversales | `StringExtensions`, `DateTimeExtensions` | Sin dependencia de módulo concreto |
| Excepciones de proyecto | `ExceptionCodes` (si son globales) | Si hay códigos de error compartidos |

---

## Qué NO va en Shared

- Entidades de dominio → van en `Domain/Entities/`
- Repositorios → van en `Infrastructure/Repositories/`
- Servicios de negocio → van en `Domain/Services/` o `Application/Services/`
- DTOs de un módulo específico → van en `Application/Dtos/`
- Configuraciones de un módulo → van en `Api/Dtos/Configurations/`

---

## Estructura de directorios

```
Shared/
├── Constants/
│   ├── ApiConstants.cs          # VERSION_1, VERSION_2
│   └── PolicyConstants.cs       # ALLOW_ALL_CORS_POLICY, FIXED_RATE_LIMITING_POLICY
├── Dtos/
│   ├── SolicitudPaginacion.cs
│   └── ListaPaginada.cs
└── Extensions/
    └── QueryableExtensions.cs   # .Paginar(), .Ordenar()
```

---

## ApiConstants

```csharp
namespace Company.Product.Module.Shared.Constants;

public static class ApiConstants
{
    public static readonly ApiVersion VERSION_1 = new(1, 0);
    public static readonly ApiVersion VERSION_2 = new(2, 0);
}
```

---

## SolicitudPaginacion y ListaPaginada

```csharp
namespace Company.Product.Module.Shared.Dtos;

public record SolicitudPaginacion(
    int Pagina = 1,
    int TamanoPagina = 10,
    string? OrdenarPor = null,
    string? Filtro = null,
    bool Descendente = false);

public class ListaPaginada<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int TotalRegistros { get; init; }
    public int Pagina { get; init; }
    public int TamanoPagina { get; init; }
    public int TotalPaginas => (int)Math.Ceiling((double)TotalRegistros / TamanoPagina);
    public bool TienePaginaAnterior => Pagina > 1;
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;
}
```

---

## Reglas

- Shared es la única capa que puede ser referenciada por todas las demás
- Shared **no** referencia ninguna otra capa del proyecto (sin dependencias circulares)
- Si un elemento solo lo usa un módulo, va en ese módulo — no en Shared
- Si un elemento lo usan dos o más módulos, evaluar si va en Shared o en el módulo más "dueño"
- En arquitectura Hermes (proyecto pequeño), Shared puede no existir — sus elementos van en Api o Domain directamente
