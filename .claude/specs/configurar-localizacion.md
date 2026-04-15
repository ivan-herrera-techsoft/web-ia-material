# Spec: Localización (SharedResources)

## Propósito

Define los contratos para el sistema de localización de mensajes de error en Bisoft Atenea. La localización traduce los códigos de excepción de `Bisoft.Exceptions` en mensajes legibles por el usuario según su cultura (header `Accept-Language`). El consumidor principal es `ErrorHandlerMiddleware`.

> **Spec relacionado:** Los mensajes se consumen en el middleware → [Spec: ErrorHandlerMiddleware](configurar-error-handler.md). Los códigos de excepción se definen en el dominio → [Rules: entities.md](../rules/entities.md).

---

## Contratos

### SC-LOC-01: `SetComponentPrefix` como primer statement de Program.cs

`TException.SetComponentPrefix(prefix)` debe ser el primer statement de `Program.cs`, antes de cualquier otra instrucción, incluyendo `WebApplication.CreateBuilder`. Si no se llama antes del primer `throw`, `Bisoft.Exceptions` lanza internamente un `TImplementationException`.

**Justificación:** el prefijo es global y estático; una vez que se lanza la primera excepción ya no puede cambiarse. Ponerlo primero garantiza que siempre esté registrado.

✅ Correcto:
```csharp
// Program.cs — primera línea
TException.SetComponentPrefix("GEN");

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .ConfigureLocalization()
    // ...
```

❌ Incorrecto:
```csharp
var builder = WebApplication.CreateBuilder(args);
// ... varios servicios registrados ...
TException.SetComponentPrefix("GEN");   // puede ser demasiado tarde si algo lanzó antes
```

---

### SC-LOC-02: SharedResources.cs debe estar vacía

La clase `SharedResources` es un marcador para que el runtime localice los archivos `.resx`. No debe tener propiedades, métodos, herencia ni atributos. Cualquier código añadido es incorrecto.

**Justificación:** `IStringLocalizerFactory.Create(typeof(SharedResources))` solo necesita el tipo para extraer el namespace y el nombre del tipo y construir la ruta al recurso. Añadir miembros no aporta nada y puede causar confusión.

✅ Correcto:
```csharp
namespace Company.Product.Module.Api.Resources;

public class SharedResources
{
}
```

❌ Incorrecto:
```csharp
public class SharedResources
{
    public static string ErrorInterno => "El servicio no está disponible...";
    // Las constantes aquí no son accesibles desde IStringLocalizer
}
```

---

### SC-LOC-03: Las claves del resx son los códigos de excepción

Cada entrada `<data name="...">` en el `.resx` debe usar exactamente el código que genera `Bisoft.Exceptions` como clave, es decir `{ComponentPrefix}{ExceptionPrefix}{NumericCode:0000}`. La clave `ErrorInterno` es la única excepción: se usa como fallback para excepciones no tipadas.

**Justificación:** `ErrorHandlerMiddleware` indexa el localizer con `_localizer[customException.Code]`. Si la clave del resx no coincide con el código exacto, el localizer devuelve la clave como valor (sin traducción).

✅ Correcto:
```xml
<!-- ComponentPrefix="GEN", TArgumentException (ARG), código 1 -->
<data name="GENARG0001" xml:space="preserve">
  <value>Valor nulo o vacío.</value>
</data>

<!-- ComponentPrefix="GEN", TInvalidOperationException (OP), código personalizado 11 -->
<data name="GENOP0011" xml:space="preserve">
  <value>El canal {Nombre} ya existe.</value>
</data>
```

❌ Incorrecto:
```xml
<!-- Sin ComponentPrefix, sin formato estándar -->
<data name="NullOrEmpty" xml:space="preserve">
  <value>Valor nulo o vacío.</value>
</data>

<!-- Código incorrecto — ARG0001 en vez de GENARG0001 -->
<data name="ARG0001" xml:space="preserve">
  <value>Valor nulo o vacío.</value>
</data>
```

---

### SC-LOC-04: Los tres archivos resx deben tener las mismas claves

`SharedResources.resx`, `SharedResources.es-MX.resx` y `SharedResources.es-GT.resx` deben contener exactamente el mismo conjunto de claves. El `.resx` base actúa como fallback cuando la cultura del cliente no coincide con ninguna variante específica.

**Justificación:** si una clave existe en `.es-MX.resx` pero no en `.es-GT.resx`, el cliente guatemalteco recibirá la clave literal como mensaje. El fallback solo actúa cuando no existe el archivo de cultura, no cuando falta una clave específica dentro de él.

✅ Correcto:
```
SharedResources.resx       → GENARG0001, GENNF0001, ErrorInterno, GENOP0011
SharedResources.es-MX.resx → GENARG0001, GENNF0001, ErrorInterno, GENOP0011
SharedResources.es-GT.resx → GENARG0001, GENNF0001, ErrorInterno, GENOP0011
```

❌ Incorrecto:
```
SharedResources.resx       → GENARG0001, GENNF0001, ErrorInterno, GENOP0011
SharedResources.es-MX.resx → GENARG0001, GENNF0001, ErrorInterno, GENOP0011
SharedResources.es-GT.resx → GENARG0001, GENNF0001, ErrorInterno
                              ↑ falta GENOP0011 — cliente GT recibe "GENOP0011" literal
```

---

### SC-LOC-05: Placeholders en resx deben coincidir con Args de la excepción

Cuando el mensaje de una excepción de negocio incluye datos variables, el resx usa placeholders con la sintaxis `{NombreClave}`. El nombre del placeholder debe ser idéntico (case-sensitive) a la clave del diccionario `Args` de la excepción.

**Justificación:** `ErrorHandlerMiddleware` reemplaza los placeholders iterando `customException.Args` con `detail.Replace($"{{{kv.Key}}}", ...)`. Si los nombres no coinciden, el placeholder queda sin reemplazar en la respuesta.

✅ Correcto:
```csharp
// Excepción lanzada con Args["Nombre"] = "mi-canal"
throw new TInvalidOperationException(
    ExceptionCodes.Operation.CANAL_ALREADY_EXISTS,
    "El canal {Nombre} ya existe",
    new Dictionary<string, object> { ["Nombre"] = nombre });
```
```xml
<!-- Placeholder {Nombre} coincide exactamente con la clave Args -->
<data name="GENOP0011" xml:space="preserve">
  <value>El canal {Nombre} ya existe.</value>
</data>
```

❌ Incorrecto:
```csharp
// Args["nombre"] en minúscula
throw new TInvalidOperationException(
    ExceptionCodes.Operation.CANAL_ALREADY_EXISTS,
    "...",
    new Dictionary<string, object> { ["nombre"] = nombre });
```
```xml
<!-- Placeholder {Nombre} con N mayúscula — no coincide, queda sin reemplazar -->
<data name="GENOP0011" xml:space="preserve">
  <value>El canal {Nombre} ya existe.</value>
</data>
```

---

### SC-LOC-06: ConfigureLocalization usa solo AcceptLanguageHeaderRequestCultureProvider

`RequestCultureProviders` debe contener únicamente `AcceptLanguageHeaderRequestCultureProvider`. No se deben agregar `QueryStringRequestCultureProvider` ni `CookieRequestCultureProvider`.

**Justificación:** la API expone un header estándar HTTP (`Accept-Language`) para negociar la cultura. Los mecanismos de query string y cookie son propios de aplicaciones web con sesión de usuario, no de APIs REST stateless.

✅ Correcto:
```csharp
options.RequestCultureProviders = new IRequestCultureProvider[]
{
    new AcceptLanguageHeaderRequestCultureProvider()
};
```

❌ Incorrecto:
```csharp
// Incluye providers que no aplican a una API stateless
options.RequestCultureProviders = new IRequestCultureProvider[]
{
    new QueryStringRequestCultureProvider(),
    new CookieRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};
```

---

### SC-LOC-07: AddLocalization sin ResourcesPath

`services.AddLocalization()` se llama sin parámetros (sin `options => options.ResourcesPath = "..."`) porque los archivos `.resx` están en la misma carpeta que `SharedResources.cs` (`Api/Resources/`).

**Justificación:** cuando `ResourcesPath` se deja vacío, el runtime busca los `.resx` en la ruta derivada del namespace y nombre del tipo. Si se especifica una ruta distinta a la real, el localizer no encuentra los recursos y devuelve las claves literales.

✅ Correcto:
```csharp
services.AddLocalization();   // busca en namespace/nombre del tipo
```

❌ Incorrecto:
```csharp
services.AddLocalization(options => options.ResourcesPath = "Resources");
// Solo funciona si todos los resx están en /Resources/ relativo al assembly root.
// Si SharedResources.cs está en Api/Resources/ con namespace Api.Resources,
// el path combinado sería incorrecto.
```

---

### SC-LOC-08: UseRequestLocalization resuelve IOptions desde el contenedor

`UseRequestLocalization` debe obtener las opciones del contenedor de DI mediante `GetRequiredService<IOptions<RequestLocalizationOptions>>().Value`, no construyendo un nuevo objeto `RequestLocalizationOptions` en línea.

**Justificación:** la configuración fue registrada en `ConfigureLocalization` vía `services.Configure<RequestLocalizationOptions>`. Obtenerla del contenedor garantiza que se usa exactamente la misma instancia configurada, sin riesgo de inconsistencias si la configuración cambia en algún middleware intermedio.

✅ Correcto:
```csharp
app.UseRequestLocalization(
    app.Services
       .GetRequiredService<IOptions<RequestLocalizationOptions>>()
       .Value);
```

❌ Incorrecto:
```csharp
// Construye un objeto nuevo, ignorando lo registrado en ConfigureLocalization
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-MX")
});
```

---

### SC-LOC-09: IStringLocalizer no genérico en ErrorHandlerMiddleware

El middleware declara `IStringLocalizer _localizer` (sin tipo genérico) y lo inicializa con `factory.Create(typeof(SharedResources))`. No se inyecta `IStringLocalizer<SharedResources>` directamente como dependencia del constructor.

**Justificación:** `ErrorHandlerMiddleware` usa el constructor clásico (no constructor principal), donde `RequestDelegate` es el primer parámetro. `IStringLocalizerFactory` se inyecta en ese constructor y se convierte a `IStringLocalizer` en el cuerpo. Inyectar `IStringLocalizer<SharedResources>` directamente también funciona, pero la convención del template es usar la factory para mantener la coherencia con el patrón de inicialización.

✅ Correcto:
```csharp
private readonly IStringLocalizer _localizer;

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

❌ Incorrecto:
```csharp
// Inyección directa del genérico — rompe el patrón del template
public ErrorHandlerMiddleware(
    LoggerWrapper<ErrorHandlerMiddleware> logger,
    RequestDelegate next,
    IStringLocalizer<SharedResources> localizer)
{
    _localizer = localizer;
}
```
