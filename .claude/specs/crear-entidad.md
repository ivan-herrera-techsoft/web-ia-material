# Spec: Entidades de Dominio

## Propósito

Define las reglas contractuales que toda entidad de dominio debe cumplir en este proyecto. Aplica a la capa `Domain/Entities/` y sus dependencias directas (`Domain/Validators/`, `Domain/DomainConstants.cs`, `Infrastructure/Mapping/`).

> **Spec relacionado:** Los archivos de mapping generados en esta spec deben registrarse en la Strategy del Context correspondiente. Ver → [Spec: DbContext de Infraestructura](crear-contexto.md) — SC-STR-04.

---

## Contratos obligatorios

### SC-ENT-01: Constructor privado

Toda entidad DEBE tener un constructor `private` sin parametros. Es requerido por EF Core para materializar instancias desde la base de datos. Este constructor **no inicializa estado de negocio**.

```csharp
private Canal() { }  // OBLIGATORIO
```

### SC-ENT-02: Constructor de creación y Guards

La creación de una entidad invoca Guards en el constructor publico llamando a los metodos `Formatear*()` del validador correspondiente. Los Guards validan restricciones simples: nulidad, longitud, formato.

```csharp
public Canal(string nombre, string descripcion) : base()
{
    Nombre = nombre.FormatearNombre();       // Guard: null + longitud
    Descripcion = descripcion.FormatearDescripcion();  // Guard: longitud opcional
    FechaCreacionUtc = DateTime.UtcNow;
    FechaActualizacionUtc = FechaCreacionUtc;
    Activo = true;
}
```

### SC-ENT-03: Patrón Creator para validaciones complejas

Cuando la creación requiere validaciones que **no pueden realizarse solo con Guards** (unicidad, estado del sistema, reglas cruzadas entre entidades), se usa un metodo estatico `Crear`. Este recibe como parametros los resultados de consultas o verificaciones realizadas previamente por el Domain Service.

```csharp
// El Domain Service consulta ANTES de llamar al Creator
var configuracionExistente = await _repositorio.ConsultarCanalPorNombre(nombre, ct)
    ?? throw TNotFoundException.EntityNotFound($"Canal con nombre: {nombre} no encontrado");
var canal = Canal.Crear(nombre, descripcion, configuracionExistente);
```

El Creator **nunca llama directamente a repositorios o servicios**; recibe los datos necesarios como argumentos.

### SC-ENT-04: Acceso a propiedades

| Tipo de propiedad | Modificador | Ejemplo |
|---|---|---|
| Inmutable (asignada en constructor, nunca cambia) | `{ get; }` | `public string Nombre { get; }` |
| Mutable (puede cambiar vía metodo de dominio) | `{ get; private set; }` | `public bool Activo { get; private set; }` |
| Navegación a entidad única | `{ get; private set; } = default!;` | `public Categoria Categoria { get; private set; } = default!;` |
| Navegación colección | `IReadOnlyCollection<T>` con campo privado | Ver SC-ENT-05 |

**Prohibido**: setters públicos en entidades de dominio.
**Prohibido**: campos que componen llaves primarias como mutables.

### SC-ENT-05: Colecciones encapsuladas

Las propiedades de navegación que representan colecciones se encapsulan mediante un campo privado de tipo `List<T>` y se exponen como `IReadOnlyCollection<T>`:

```csharp
private readonly List<TipoEvento> _tiposEvento = [];
public IReadOnlyCollection<TipoEvento> TiposEvento => _tiposEvento.AsReadOnly();
```

La mutación de la colección se realiza unicamente vía metodos de dominio de la entidad:

```csharp
public void AgregarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Add(tipoEvento);
public void QuitarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Remove(tipoEvento);
```

EF Core debe configurarse con `UsePropertyAccessMode(PropertyAccessMode.Field)` y `HasField("_campo")` en el Mapping para acceder al campo privado. Además, las llaves primarias de la entidad que pertenece a la colección deben tener forzosamente la configuración `ValueGeneratedOnAddNever()` en caso de que no sean valores autogenerados.

### SC-ENT-06: Métodos de dominio

Todo cambio de estado de una entidad se realiza mediante métodos de la entidad. Un método de dominio representa un **evento de negocio** con nombre descriptivo en español:

- `Actualizar(nombre, descripcion)` — edición de datos básicos
- `Alternar()` — toggle de estado activo/inactivo
- `CambiarPassword(anterior, nueva)` — cambio de credencial con validación
- `AgregarHijo(hijo)` / `QuitarHijo(hijo)` — mutación de colección

Los métodos de dominio actualizan `FechaActualizacionUtc = DateTime.UtcNow` cuando aplique.

### SC-ENT-07: Fechas

- Siempre UTC: usar `DateTime.UtcNow`
- Convenciones de nombre: `FechaCreacionUtc`, `FechaActualizacionUtc`
- La conversion a hora local se realiza SOLO en la capa de presentación
- En caso de ser la fecha en Utc agregar sufijo `Utc`.

### SC-ENT-08: Inputs

Utilizar clases Inputs inmutables, con sus validaciones en caso de que se requieran más de 6 parámetros de entrada para crear una entidad. Este deberá de incluir las validaciones de Guards en su constructor en lugar del de la entidad, de forma que al ya existir un Input, sus valores ya deben de estar formateados y validados.

Estos se encuentran en la carpeta `Domain/Entities/Inputs`.

---

## Contratos del validador

### SC-VAL-01: Ubicación y estructura

- Ubicación: `Domain/Validators/Entities/{Entidad}Validator.cs`
- Clase `static`
- Solo contiene extension methods sobre tipos primitivos (`string?`, `int?`, etc.)

### SC-VAL-02: Patrón de nombre

`Formatear{Campo}(this string? valor)` — en español, sin sufijo Async.

### SC-VAL-03: Uso de StringValidator y Bisoft.Exceptions

Los métodos de validación usan el helper `StringValidator` del proyecto y producen excepciones de `Bisoft.Exceptions`:

```
ValidateNull   → TArgumentException.NullOrEmpty
ValidateLength → TArgumentException.OutOfRange
ValidateRegex  → TArgumentException.InvalidFormat
ValidateEmail  → TArgumentException.InvalidFormat
```

En caso de necesitar codigos de error especificos por cada validación, establecerlos y enviarlos como primer parámetro usando el constructor de `TArgumentException()`. Ver SC-CONST-01 y SC-CONST-02.

### SC-VAL-04: Uso de constantes de DomainConstants

Todo límite de longitud referencia una constante de `DomainConstants.Values`. No se permiten literales numericos para longitudes en validadores ni en mappings.

```csharp
// CORRECTO
.ValidateLength(maxLength: Values.MAX_LENGTH_NOMBRE_CANAL, ...)

// INCORRECTO
.ValidateLength(maxLength: 256, ...)
```

### SC-VAL-05: Retorno del valor formateado

Todo metodo `Formatear*()` aplica `Trim()` o cambio de casing **antes** de las validaciones, regresando el valor ya con formato y validado. Los campos opcionales retornan `null` cuando el valor es permitido nulo o vacío.

---

## Contratos del mapping EF Core

> **Ver también** → [Spec: DbContext de Infraestructura](crear-contexto.md) — SC-STR-04: cada clase de mapping creada aquí debe registrarse en la Strategy del Context al que pertenece la entidad.

### SC-MAP-01: Separación por proveedor

Un archivo de configuración por proveedor de BD: `SqlServer/`, `Postgres/`, `Sqlite/`. La clase es `internal`.

### SC-MAP-02: Tipos de columna tipados

Usar las constantes de `Bisoft.DatabaseConnections.ColumnTypes` (`SqlServerColumnTypes`, `PostgresColumnTypes`, `SqliteColumnTypes`). No usar strings literales para tipos de columna.

### SC-MAP-03: MaxLength desde constantes

`HasMaxLength(Values.MAX_LENGTH_{CAMPO})` — nunca un literal numerico.

### SC-MAP-04: Configuración de colecciones encapsuladas

```csharp
builder.HasMany(e => e.Hijos)
    .WithOne(h => h.Padre)              // Opcional si la clase hija tiene referencia al padre
    .HasForeignKey(x => x.PadreId)
    .HasPrincipalKey(x => x.Id)
    .IsRequired()                       // false en caso de FK nullable
    .OnDelete(DeleteBehavior.Cascade);  // Ajustar según comportamiento de borrado
```

---

## Contratos de DomainConstants

### SC-CONST-01: Estructura

```csharp
public static class DomainConstants
{
    public static class ExceptionCodes
    {
        public static class Operation
        {
            // Códigos de errores de negocio (TInvalidOperationException)
            // Enteros >= 11, únicos en todo el proyecto
            public const int USUARIO_DESACTIVADO = 11;
        }
        public static class Argument
        {
            // Códigos de errores específicos de argumentos (TArgumentException con código)
            // Enteros >= 11, únicos en todo el proyecto, serie independiente de Operation
            public const int FORMATO_INVALIDO_DE_NOMBRE_USUARIO = 11;
        }
    }

    public static class Values
    {
        // Constantes de longitud maxima y valores de negocio
        // Nomenclatura: MAX_LENGTH_{CAMPO}_{ENTIDAD} o MAX_LENGTH_{CAMPO} si es compartido
        public const int MAX_LENGTH_NOMBRE_USUARIO = 50;
    }
}
```

### SC-CONST-02: Códigos de excepción

Los códigos personalizados de `TInvalidOperationException` son enteros únicos en todo el proyecto, comenzando desde 11. Se declaran en `ExceptionCodes.Operation`.

Los códigos personalizados de `TArgumentException` (cuando se necesita identificar específicamente el campo con error) son enteros únicos en todo el proyecto, comenzando desde 11. Se declaran en `ExceptionCodes.Argument`. La serie es independiente de la de `Operation`.

---

## Criterios de aceptación

Al crear o revisar una entidad, verificar:

- [ ] Asigna Id autogenerado al crearlo por constructor o no dependiendo si es `Entity` o `AggregateRootEntity` 
- [ ] Hereda de clases base aplicando DRY con clases abstractas según si comparte comportamiento con otras clases
- [ ] Tiene constructor `private` sin parametros
- [ ] Propiedades inmutables solo tienen `{ get; }`
- [ ] Propiedades mutables tienen `{ get; private set; }`
- [ ] Campos que componen llaves primarias no son mutables
- [ ] Colecciones usan `IReadOnlyCollection<T>` + campo privado `List<T>`
- [ ] Navegacion unica tiene `{ get; private set; } = default!;`
- [ ] Constructor público llama `Formatear*()` de su validador
- [ ] `Formatear*()` aplica `Trim()` / casing **antes** de validar
- [ ] Si hay reglas de negocio complejas, existe un metodo `Crear` estático
- [ ] Cambios de estado solo ocurren en métodos de dominio
- [ ] Fechas UTC usan sufijo `Utc` en el nombre de la propiedad
- [ ] Validador usa `StringValidator` y constantes de `DomainConstants.Values`
- [ ] Mapping configura `HasMaxLength` con constantes (no literales)
- [ ] Mapping usa tipos de columna tipados (`SqlServerColumnTypes`, etc.)
- [ ] Colecciones en mapping usan `HasMany().WithOne()...`
- [ ] PKs no autogeneradas en entidades hijo tienen `ValueGeneratedOnAddNever()`
- [ ] Los archivos de mapping están registrados en la Strategy del Context → ver [crear-contexto.md](crear-contexto.md)
