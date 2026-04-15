---
description: Configuracion de localizacion con SharedResources y resx en Bisoft Atenea
globs: "**/Resources/**,**/Middlewares/ErrorHandlerMiddleware.cs,**/Extensions/ServiceExtensions.cs"
---

## Propósito

La localización permite que los mensajes de error devueltos por el `ErrorHandlerMiddleware` se adapten al idioma del cliente según el header `Accept-Language`. Las claves del resx son los códigos de excepción del paquete `Bisoft.Exceptions`.

---

## Archivos involucrados

```
Api/
└── Resources/
    ├── SharedResources.cs           ← clase marcador (vacía)
    ├── SharedResources.resx         ← textos por defecto (es-MX)
    ├── SharedResources.es-MX.resx   ← cultura México
    └── SharedResources.es-GT.resx   ← cultura Guatemala
```

---

## SharedResources.cs — clase marcador

La clase es intencionalmente vacía. Solo existe para que el runtime sepa en qué ensamblado y carpeta buscar los archivos `.resx`. `IStringLocalizerFactory.Create(typeof(SharedResources))` usa su namespace y nombre para localizar el recurso.

```csharp
namespace Company.Product.Module.Api.Resources;

public class SharedResources
{
}
```

No tiene propiedades, métodos ni herencia. No se debe agregar nada.

---

## Claves en el resx — formato

Las claves del `.resx` son los **códigos de excepción** que produce `Bisoft.Exceptions`. El código se construye como `{ComponentPrefix}{ExceptionPrefix}{NumericCode:0000}`.

El `ComponentPrefix` se registra en `Program.cs`:
```csharp
TException.SetComponentPrefix("GEN");   // primer statement de Program.cs
```

Los prefijos de cada tipo de excepción son fijos:

| Tipo                          | ExceptionPrefix | Ejemplo de código |
|-------------------------------|-----------------|-------------------|
| `TArgumentException`          | `ARG`           | `GENARG0001`      |
| `TNotFoundException`          | `NF`            | `GENNF0001`       |
| `TInvalidOperationException`  | `OP`            | `GENOP0001`       |
| `TUnauthorizedAccessException`| `UA`            | `GENUA0001`       |
| `TEnvironmentException`       | `ENV`           | `GENENV0001`      |

Los factory methods genéricos ya tienen números reservados del `0001` al `0009`. Los **códigos personalizados** de negocio usan números a partir del `0011`.

---

## Claves base obligatorias en el resx

Estas claves deben existir en todos los proyectos:

```xml
<!-- Fallback para excepciones no controladas -->
<data name="ErrorInterno" xml:space="preserve">
  <value>El servicio no está disponible, comuníquese con soporte.</value>
</data>

<!-- TUnauthorizedAccessException -->
<data name="GENUA0001" xml:space="preserve">
  <value>Credenciales incorrectas.</value>
</data>
<data name="GENUA0002" xml:space="preserve">
  <value>Permisos insuficientes.</value>
</data>
<data name="GENUA0003" xml:space="preserve">
  <value>Token inválido.</value>
</data>

<!-- TArgumentException -->
<data name="GENARG0001" xml:space="preserve">
  <value>Valor nulo o vacío.</value>
</data>
<data name="GENARG0002" xml:space="preserve">
  <value>Formato inválido.</value>
</data>
<data name="GENARG0003" xml:space="preserve">
  <value>Valor fuera de rango.</value>
</data>
<data name="GENARG0004" xml:space="preserve">
  <value>Valor no soportado.</value>
</data>

<!-- TNotFoundException -->
<data name="GENNF0001" xml:space="preserve">
  <value>Entidad no encontrada.</value>
</data>
<data name="GENNF0002" xml:space="preserve">
  <value>Recurso no encontrado.</value>
</data>
```

> Cambiar el prefijo `GEN` por el `ComponentPrefix` real del proyecto.

---

## Claves con placeholders (excepciones de negocio)

Para `TInvalidOperationException` con argumentos, la clave del resx puede contener placeholders `{NombreClave}`. El `ErrorHandlerMiddleware` los reemplaza iterando `customException.Args`:

```xml
<!-- La excepción se lanza con Args["User"] = "kiyoshi" -->
<data name="GENOP0001" xml:space="preserve">
  <value>El usuario {User} ya existe.</value>
</data>
```

El placeholder debe coincidir exactamente (case-sensitive) con la clave del diccionario `Args` de la excepción.

---

## ConfigureLocalization — registro en DI

```csharp
private static readonly string[] _supportedLanguages = ["es-MX", "es-GT"];

public static IServiceCollection ConfigureLocalization(this IServiceCollection services)
{
    services.AddLocalization();
    var supportedCultures = _supportedLanguages.Select(c => new CultureInfo(c)).ToList();
    services.Configure<RequestLocalizationOptions>(options =>
    {
        options.SupportedCultures   = supportedCultures;
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

Puntos clave:
- `AddLocalization()` sin `ResourcesPath` — los resx están junto a `SharedResources.cs`, en la misma carpeta `Resources/`.
- Solo `AcceptLanguageHeaderRequestCultureProvider` — el cliente envía `Accept-Language: es-GT`.
- Cultura por defecto `es-MX`; si el header no coincide con ninguna soportada, se usa el default.

---

## UseRequestLocalization — pipeline

Debe registrarse en el pipeline de `WebApplication` **antes** de los endpoints:

```csharp
app.UseRequestLocalization(
    app.Services
       .GetRequiredService<IOptions<RequestLocalizationOptions>>()
       .Value);
```

Se obtiene el `IOptions<RequestLocalizationOptions>` ya registrado por `ConfigureLocalization` para evitar duplicar la configuración.

---

## Uso en ErrorHandlerMiddleware

El middleware recibe `IStringLocalizerFactory` (no `IStringLocalizer<T>`) y lo convierte con `Create(typeof(SharedResources))`:

```csharp
public ErrorHandlerMiddleware(
    LoggerWrapper<ErrorHandlerMiddleware> logger,
    RequestDelegate next,
    IStringLocalizerFactory factory)
{
    _logger   = logger;
    _next     = next;
    _localizer = factory.Create(typeof(SharedResources));
}
```

Para obtener el mensaje localizado se indexa por código de excepción:

```csharp
// Para TException (excepción tipada de Bisoft):
var template = _localizer[customException.Code];      // clave = código, ej. "GENOP0001"
string detail = template.Value;

// Reemplazar placeholders con Args:
if (customException.Args != null)
{
    foreach (var kv in customException.Args)
        detail = detail.Replace($"{{{kv.Key}}}", kv.Value?.ToString());
}

// Para BadHttpRequestException / excepciones no controladas:
var template = _localizer["ErrorInterno"];
error.Message = template.Value;
```

---

## Agregar una excepción de negocio nueva

1. Definir el código en `Domain/DomainConstants.cs`:
   ```csharp
   public static class ExceptionCodes
   {
       public static class Operation
       {
           public const int CANAL_ALREADY_EXISTS = 11;   // → GENOP0011
       }
   }
   ```

2. Lanzar la excepción en el Domain Service:
   ```csharp
   throw new TInvalidOperationException(
       ExceptionCodes.Operation.CANAL_ALREADY_EXISTS,
       "El canal {Nombre} ya existe",
       new Dictionary<string, object> { ["Nombre"] = nombre });
   ```

3. Agregar la clave en los tres resx (`SharedResources.resx`, `.es-MX.resx`, `.es-GT.resx`):
   ```xml
   <data name="GENOP0011" xml:space="preserve">
     <value>El canal {Nombre} ya existe.</value>
   </data>
   ```

---

## SetComponentPrefix — obligatorio en Program.cs

Debe ser el primer statement de `Program.cs`, antes del builder:

```csharp
TException.SetComponentPrefix("GEN");   // ← primera línea

var builder = WebApplication.CreateBuilder(args);
// ...
```

Sin esta llamada, el primer throw de cualquier `TException` lanza internamente un `TImplementationException`.
