---
description: Convenciones para entidades de dominio
globs: "**/Entities/**,**/Domain/**,**/Validators/**"
---

## Entidades de dominio

### Ubicacion y herencia

- Ubicacion: `Domain/Entities/{Modulo}/{Entidad}.cs`
- Toda entidad hereda de `Entity` o `AggregateRootEntity` (paquete interno)
  - `Entity` — genera su propio `Guid Id` en el constructor protegido. Usar para entidades raiz
  - `AggregateRootEntity` — `Id = Guid.Empty` en constructor; EF Core lo rellena desde la BD. Usar para entidades hijo dentro de un agregado

### Propiedades

- `Id` proviene de la clase base (no redeclarar)
- Propiedades **inmutables** (solo asignadas en constructor): solo `get` sin setter
- Propiedades **mutables** (modificables via metodos de dominio): `private set`
- **Nunca** exponer setters publicos

```csharp
public string Nombre { get; }          // inmutable
public bool Activo { get; private set; } // mutable
```

### Propiedades de navegacion

- **Referencia unica** (otro agregado o entidad padre): `private set`, inicializar con `default!`
- **Coleccion**: encapsular con `IReadOnlyCollection<T>` respaldado por `List<T>` privado

```csharp
// Referencia unica — solo private set
public Categoria Categoria { get; private set; } = default!;

// Coleccion — readonly encapsulada
private readonly List<TipoEvento> _tiposEvento = [];
public IReadOnlyCollection<TipoEvento> TiposEvento => _tiposEvento.AsReadOnly();
```

### Constructor

- Constructor `protected` sin parametros, siempre presente (requerido por EF Core)
- Constructor publico con parametros: para creacion cuando las validaciones son **solo Guards**
  - Llamar `: base()` para que `Entity` genere el `Id`
  - Invocar los metodos `Formatear*()` del validador sobre cada campo obligatorio

```csharp
protected Canal() { }

public Canal(string nombre, string? descripcion) : base()
{
    Nombre = nombre.FormatearNombre();
    Descripcion = descripcion?.FormatearDescripcion();
    FechaCreacion = DateTime.UtcNow;
    FechaActualizacion = FechaCreacion;
    Activo = true;
}
```

### Patron Creator (validaciones complejas)

Cuando la creacion requiere **validaciones de negocio mas alla de Guards** (unicidad, reglas cruzadas, estado del sistema), usar un metodo estatico `Crear` en lugar del constructor directamente. El Creator puede recibir dependencias adicionales o resultados de consultas previas como parametros:

```csharp
public static Canal Crear(string nombre, bool canalYaExiste)
{
    if (canalYaExiste)
        throw new TInvalidOperationException(
            ExceptionCodes.Operation.CANAL_YA_EXISTE,
            "Ya existe un canal con el nombre {Nombre}",
            new Dictionary<string, object> { ["Nombre"] = nombre }
        );
    return new Canal(nombre, descripcion: null);
}
```

### Metodos de dominio

- Todo cambio de estado se realiza mediante metodos de la entidad, nunca asignando propiedades desde fuera
- Fechas de actualizacion se actualizan dentro del metodo de dominio

```csharp
public void Actualizar(string nombre, string? descripcion)
{
    Nombre = nombre.FormatearNombre();
    Descripcion = descripcion?.FormatearDescripcion();
    FechaActualizacion = DateTime.UtcNow;
}

public void Alternar() => Activo = !Activo;

// Metodos para colecciones encapsuladas
public void AgregarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Add(tipoEvento);
public void QuitarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Remove(tipoEvento);
```

### Fechas

- Siempre en UTC: `FechaCreacion`, `FechaActualizacion` (sin sufijo en español)
- Inicializar con `DateTime.UtcNow`

---

## Validadores de dominio

- Ubicacion: `Domain/Validators/Entities/{Entidad}Validator.cs`
- Clase `static` con extension methods
- Patron de nombre: **`Formatear{Campo}(this string? valor)`**
- Usar `StringValidator` (del paquete interno) para encadenar validaciones:
  - `.ValidateNull(excepcion)` — verifica nulo o vacio
  - `.ValidateLength(minLength, maxLength, excepcion)` — valida longitud
  - `.ValidateRegex(patron, excepcion)` — valida formato
  - `.ValidateEmail(excepcion)` — valida formato email
- Usar **constantes de `DomainConstants.Values`** para los limites de longitud
- Usar tipos de **`Bisoft.Exceptions`** para las excepciones:
  - `StringValidator.ExceptionWhenNullArgument("campo")` — `TArgumentException.NullOrEmpty`
  - `StringValidator.ExceptionWhenOutOfRangeArguments("campo", maxLength)` — `TArgumentException.OutOfRange`
  - `TArgumentException.InvalidFormat("mensaje")` — formato incorrecto (regex, email)
- Retornar el valor `Trim()`-ado si la validacion pasa

```csharp
using Bisoft.Exceptions;
using static Empresa.Producto.Modulo.Domain.DomainConstants;

namespace Empresa.Producto.Modulo.Domain.Validators.Entities;

public static class CanalValidator
{
    public static string FormatearNombre(this string? nombre)
    {
        nombre.ValidateNull(StringValidator.ExceptionWhenNullArgument("nombre"))
              .ValidateLength(
                  minLength: 1,
                  maxLength: Values.MAX_LENGTH_NOMBRE_CANAL,
                  exceptionWhenInvalid: StringValidator.ExceptionWhenOutOfRangeArguments("nombre", Values.MAX_LENGTH_NOMBRE_CANAL)
              );
        return nombre!.Trim();
    }

    public static string? FormatearDescripcion(this string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion)) return null;
        descripcion.ValidateLength(
            maxLength: Values.MAX_LENGTH_DESCRIPCION_CANAL,
            exceptionWhenInvalid: StringValidator.ExceptionWhenOutOfRangeArguments("descripcion", Values.MAX_LENGTH_DESCRIPCION_CANAL)
        );
        return descripcion.Trim();
    }

    public static string FormatearEmail(this string? email)
    {
        email.ValidateNull(StringValidator.ExceptionWhenNullArgument("email"))
             .ValidateEmail(TArgumentException.InvalidFormat("El email no tiene un formato valido"));
        return email!.Trim().ToLower();
    }
}
```

---

## DomainConstants

- Ubicacion: `Domain/DomainConstants.cs`
- `Values` — constantes de longitud y valores de negocio
- `ExceptionCodes.Operation` — codigos de errores de negocio (enteros, desde 11)

```csharp
public static class DomainConstants
{
    public static class ExceptionCodes
    {
        public static class Operation
        {
            public const int CANAL_YA_EXISTE = 11;
            public const int CANAL_INACTIVO = 12;
        }
    }

    public static class Values
    {
        public const int MAX_LENGTH_NOMBRE_CANAL = 256;
        public const int MAX_LENGTH_DESCRIPCION_CANAL = 512;
    }
}
```

---

## Excepciones de Bisoft.Exceptions

| Tipo | Uso | Constructor / Factory |
|------|----|----------------------|
| `TArgumentException` | Argumentos invalidos (null, rango, formato) | `.NullOrEmpty(msg)`, `.OutOfRange(msg)`, `.InvalidFormat(msg)` |
| `TInvalidOperationException` | Reglas de negocio violadas | `new(code, msg, dict)` |
| `TUnauthorizedAccessException` | Acceso no autorizado | `.IncorrectCredentials(msg)` |
| `TImplementationException` | Errores de implementacion (null entity, etc.) | `.ReferencedANullEntity()` |

---

## Mapping EF Core (Infrastructure)

- Ubicacion: `Infrastructure/Mapping/{Modulo}/{Provider}/{Entidad}{Provider}Configuration.cs`
- Clase `internal` que implementa `IEntityTypeConfiguration<T>`
- Separar por proveedor: `SqlServer/`, `Postgres/`, `Sqlite/`
- Usar **`Bisoft.DatabaseConnections.ColumnTypes`** para los tipos de columna
- Usar las constantes de **`DomainConstants.Values`** para los `HasMaxLength()`
- Configurar `ToTable("nombre", "schema")`

```csharp
using Bisoft.DatabaseConnections.ColumnTypes;
using static Empresa.Producto.Modulo.Domain.DomainConstants;

namespace Empresa.Producto.Modulo.Infrastructure.Mapping.Canales.SqlServer;

internal class CanalSqlServerConfiguration : IEntityTypeConfiguration<Canal>
{
    public void Configure(EntityTypeBuilder<Canal> builder)
    {
        builder.ToTable("Canales", "cnl")
            .HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.Nombre)
            .HasColumnType(SqlServerColumnTypes.NVARCHAR)
            .HasColumnName("nombre")
            .HasMaxLength(Values.MAX_LENGTH_NOMBRE_CANAL)
            .IsRequired();

        builder.Property(c => c.Activo)
            .HasColumnType(SqlServerColumnTypes.BIT)
            .HasColumnName("activo")
            .HasDefaultValue(true);

        builder.Property(c => c.FechaCreacion)
            .HasColumnType(SqlServerColumnTypes.DATETIME2)
            .HasColumnName("fechaCreacion")
            .IsRequired();

        // Coleccion encapsulada: configurar field de respaldo
        builder.Navigation(c => c.TiposEvento)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_tiposEvento");
    }
}
```

---

## Ejemplo completo — Entidad Canal

```csharp
using Bisoft.Exceptions;
using Bisoft.Templates.Atenea.MinimalApi.Domain.Entities.Base;
using Bisoft.Templates.Atenea.MinimalApi.Domain.Validators.Entities;
using static Bisoft.Templates.Atenea.MinimalApi.Domain.DomainConstants;

namespace Empresa.Producto.Modulo.Domain.Entities.Canales;

public class Canal : Entity
{
    // Propiedades inmutables
    public string Nombre { get; }
    public DateTime FechaCreacion { get; }

    // Propiedades mutables
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaActualizacion { get; private set; }

    // Referencia unica (navegacion)
    public Categoria Categoria { get; private set; } = default!;

    // Coleccion encapsulada
    private readonly List<TipoEvento> _tiposEvento = [];
    public IReadOnlyCollection<TipoEvento> TiposEvento => _tiposEvento.AsReadOnly();

    // Constructor para EF Core
    protected Canal() { }

    // Constructor para creacion con Guards
    public Canal(string nombre, string? descripcion) : base()
    {
        Nombre = nombre.FormatearNombre();
        Descripcion = descripcion?.FormatearDescripcion();
        FechaCreacion = DateTime.UtcNow;
        FechaActualizacion = FechaCreacion;
        Activo = true;
    }

    // Creator para validaciones de negocio complejas
    public static Canal Crear(string nombre, string? descripcion, bool yaExiste)
    {
        if (yaExiste)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.CANAL_YA_EXISTE,
                "Ya existe un canal registrado con el nombre {Nombre}",
                new Dictionary<string, object> { ["Nombre"] = nombre }
            );
        return new Canal(nombre, descripcion);
    }

    // Metodos de dominio
    public void Actualizar(string nombre, string? descripcion)
    {
        Nombre = nombre.FormatearNombre();
        Descripcion = descripcion?.FormatearDescripcion();
        FechaActualizacion = DateTime.UtcNow;
    }

    public void Alternar() => Activo = !Activo;

    public void AgregarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Add(tipoEvento);
    public void QuitarTipoEvento(TipoEvento tipoEvento) => _tiposEvento.Remove(tipoEvento);
}
```

---

## Reglas de revision de codigo existente

- Al revisar codigo con setters publicos, NO forzar el cambio; sugerir en un refactor planificado
- Si no existen metodos de dominio para mutacion, SI sugerirlos
- Si el validador usa strings directos en lugar de constantes, sugerir extraerlos a `DomainConstants.Values`
