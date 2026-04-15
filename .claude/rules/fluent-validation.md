---
description: Convenciones para FluentValidation — validación de DTOs de entrada en endpoints Minimal API
globs: "**/Endpoints/**,**/Validators/**,**/Dtos/**"
---

## Rol de FluentValidation

FluentValidation valida los **DTOs de request** que llegan al endpoint antes de que el Domain Service los procese. No reemplaza los Guards de dominio — ambos coexisten:

| Capa | Responsabilidad | Herramienta |
|---|---|---|
| Endpoint | Formato, tipos, longitud básica de campos del request | FluentValidation |
| Entidad | Reglas de negocio, invariantes de dominio | Guards (`Formatear*()`) |
| Domain Service | Unicidad, reglas cruzadas | `TInvalidOperationException` |

---

## Estructura del validator

- Ubicacion: junto al endpoint que lo usa, en `Api/Endpoints/{Modulo}/`
- Nombre: `{Operacion}{Entidad}Validator.cs` — ej. `CrearCanalValidator.cs`
- Hereda de `AbstractValidator<TRequest>`
- Constructor sin parámetros — sin inyección de dependencias

```csharp
namespace Company.Product.Module.Api.Endpoints.Canales;

public class CrearCanalValidator : AbstractValidator<CrearCanalRequest>
{
    public CrearCanalValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es requerido")
            .MaximumLength(DomainConstants.Values.MAX_LENGTH_NOMBRE_CANAL)
                .WithMessage($"El nombre no puede superar {DomainConstants.Values.MAX_LENGTH_NOMBRE_CANAL} caracteres");

        RuleFor(x => x.Descripcion)
            .MaximumLength(DomainConstants.Values.MAX_LENGTH_DESCRIPCION_CANAL)
                .WithMessage($"La descripcion no puede superar {DomainConstants.Values.MAX_LENGTH_DESCRIPCION_CANAL} caracteres");
    }
}
```

**Reglas:**
- Usar constantes de `DomainConstants.Values` para longitudes — nunca números literales
- Mensajes de error en español, descriptivos
- No llamar a repositorios ni servicios dentro del validator
- No validar unicidad — eso es responsabilidad del Domain Service

---

## Registro en DI

Los validators se registran globalmente en `ConfigureServices()`:

```csharp
services.AddValidatorsFromAssemblyContaining<Program>();
```

No se registran uno a uno. Esta llamada escanea todos los `AbstractValidator<T>` del assembly.

---

## Aplicación en el endpoint

El validator se aplica como filtro usando `.WithParameterValidation()` o manualmente:

```csharp
app.MapPost("/canales", async (
    [FromBody] CrearCanalRequest solicitud,
    IValidator<CrearCanalRequest> validator,
    ICanalService servicio,
    CancellationToken ct) =>
{
    var resultado = await validator.ValidateAsync(solicitud, ct);
    if (!resultado.IsValid)
        return Results.ValidationProblem(resultado.ToDictionary());

    var canal = await servicio.Guardar(solicitud, ct);
    return Results.Created($"/canales/{canal.Id}", canal);
})
.HasApiVersion(ApiConstants.VERSION_1);
```

O con el filtro automático (si el proyecto lo configura en `ConfigureServices`):

```csharp
app.MapPost("/canales", handler)
   .WithParameterValidation()
   .HasApiVersion(ApiConstants.VERSION_1);
```

---

## Respuesta de error de validación

FluentValidation retorna `ValidationProblem` (HTTP 400) con el diccionario de errores. El `ErrorHandlerMiddleware` **no** intercepta estos errores — FluentValidation los maneja directamente antes de llegar al handler.

---

## Lo que FluentValidation NO valida

- Unicidad (duplicados en BD) → `TInvalidOperationException` en Domain Service
- Estado de entidades → Guards en constructor de entidad
- Reglas de negocio complejas → Domain Service
- Autenticación o autorización → middleware de auth
