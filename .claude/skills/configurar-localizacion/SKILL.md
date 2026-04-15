---
name: configurar-localizacion
description: Configura el sistema de localización SharedResources con resx para mensajes de error multilenguaje en Bisoft Atenea
---

## Paso 1 — SetComponentPrefix en Program.cs

El primer statement de `Program.cs` debe registrar el prefijo del componente para `Bisoft.Exceptions`. Sin esta llamada, el primer `throw` de cualquier `TException` falla internamente.

```csharp
TException.SetComponentPrefix("GEN");   // reemplazar "GEN" por el prefijo del proyecto

var builder = WebApplication.CreateBuilder(args);
```

El prefijo forma parte de todos los códigos de excepción: `GEN` + `ARG` + `0001` = `GENARG0001`.

---

## Paso 2 — Crear SharedResources.cs

Crear `Api/Resources/SharedResources.cs`. La clase es intencionalmente vacía — solo sirve como marcador para que el runtime localice los archivos `.resx`:

```csharp
namespace Company.Product.Module.Api.Resources;

public class SharedResources
{
}
```

No agregar propiedades, herencia ni métodos.

---

## Paso 3 — Crear los archivos .resx

Crear tres archivos en `Api/Resources/`:

- `SharedResources.resx` — textos por defecto (sirve como fallback)
- `SharedResources.es-MX.resx` — variante México
- `SharedResources.es-GT.resx` — variante Guatemala

Los tres archivos deben tener el **mismo conjunto de claves**. Las claves son los códigos de excepción que genera `Bisoft.Exceptions`. Contenido base para los tres:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>

  <!-- Fallback para excepciones no controladas -->
  <data name="ErrorInterno" xml:space="preserve">
    <value>El servicio no está disponible, comuníquese con soporte.</value>
  </data>

  <!-- TUnauthorizedAccessException (prefijo UA) -->
  <data name="GENUA0001" xml:space="preserve">
    <value>Credenciales incorrectas.</value>
  </data>
  <data name="GENUA0002" xml:space="preserve">
    <value>Permisos insuficientes.</value>
  </data>
  <data name="GENUA0003" xml:space="preserve">
    <value>Token inválido.</value>
  </data>

  <!-- TArgumentException (prefijo ARG) -->
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

  <!-- TNotFoundException (prefijo NF) -->
  <data name="GENNF0001" xml:space="preserve">
    <value>Entidad no encontrada.</value>
  </data>
  <data name="GENNF0002" xml:space="preserve">
    <value>Recurso no encontrado.</value>
  </data>

  <!-- TInvalidOperationException personalizadas (prefijo OP, desde 0011) -->
  <!-- Ejemplo con placeholder: -->
  <!-- <data name="GENOP0011" xml:space="preserve">
    <value>El {Entidad} {Nombre} ya existe.</value>
  </data> -->
</root>
```

Reemplazar el prefijo `GEN` por el `ComponentPrefix` del proyecto en todos los nombres de clave.

---

## Paso 4 — Implementar ConfigureLocalization

En `Api/Extensions/ServiceExtensions.cs`, agregar el método de extensión:

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

Agregar los `using` necesarios:
```csharp
using System.Globalization;
using Microsoft.AspNetCore.Localization;
```

---

## Paso 5 — Registrar en Program.cs

**En la configuración de servicios** (`builder.Services`):

```csharp
builder.Services
    // ...
    .ConfigureLocalization()
    // ...
```

**En el pipeline** (`WebApplication`), antes de los endpoints:

```csharp
app.UseRequestLocalization(
    app.Services
       .GetRequiredService<IOptions<RequestLocalizationOptions>>()
       .Value);
```

El orden en el pipeline importa: `UseRequestLocalization` debe ir antes de `UseMiddleware<ErrorHandlerMiddleware>()`.

---

## Paso 6 — Agregar claves para nuevas excepciones de negocio

Cada vez que se define una nueva `TInvalidOperationException` de negocio en `DomainConstants`:

1. Identificar el código que generará. Ejemplo: `ComponentPrefix="GEN"`, tipo `OP`, número `11` → clave `GENOP0011`.

2. Agregar la entrada en los **tres archivos resx**:

```xml
<data name="GENOP0011" xml:space="preserve">
  <value>El canal {Nombre} ya existe.</value>
</data>
```

3. El placeholder `{Nombre}` debe coincidir exactamente (case-sensitive) con la clave del diccionario `Args` que se pasa al lanzar la excepción:

```csharp
throw new TInvalidOperationException(
    ExceptionCodes.Operation.CANAL_ALREADY_EXISTS,   // → número 11
    "El canal {Nombre} ya existe",
    new Dictionary<string, object> { ["Nombre"] = nombre });
```

---

## Checklist

- [ ] `TException.SetComponentPrefix("XXX")` es el primer statement de `Program.cs`
- [ ] `SharedResources.cs` existe en `Api/Resources/` y está vacía
- [ ] Los tres archivos resx existen (`SharedResources.resx`, `.es-MX.resx`, `.es-GT.resx`)
- [ ] Todas las claves base están presentes en los tres resx (`ErrorInterno`, `{PREFIX}UA0001`..`0003`, `{PREFIX}ARG0001`..`0004`, `{PREFIX}NF0001`..`0002`)
- [ ] `ConfigureLocalization()` llamado en `builder.Services`
- [ ] `UseRequestLocalization()` en el pipeline de `app`, antes de los endpoints
- [ ] Cada nueva excepción de negocio tiene su clave en los tres resx
