---
name: crear-entidad
description: Scaffolda una entidad de dominio completa — clase, validador, constantes, mapping EF Core e interfaz de repositorio
argument-hint: <NombreEntidad en español> ej. "Canal", "TipoEvento", "Producto"
---

Crear la entidad de dominio **$ARGUMENTS** siguiendo los patrones del proyecto.

Antes de generar cualquier archivo, **preguntar al desarrollador**:
1. ¿Cuales son las propiedades de la entidad? (nombre, tipo, si es obligatoria, si es mutable)
2. ¿La entidad tiene relaciones de navegacion? (referencia unica o coleccion hacia otra entidad)
3. ¿La creacion requiere validaciones de negocio complejas mas alla de Guards? (unicidad, estado del sistema, reglas cruzadas)
4. ¿En que modulo/schema de BD vive? (para configurar `ToTable`)
5. ¿Que proveedores de BD usa el proyecto? (SqlServer, Postgres, Sqlite o combinacion)

---

## Paso 1 — Constantes de dominio

Agregar en `Domain/DomainConstants.cs` (si no existen, o extender las existentes):

```csharp
public static class DomainConstants
{
    public static class ExceptionCodes
    {
        public static class Operation
        {
            // Codigos para reglas de negocio (TInvalidOperationException)
            // Enteros >= 11, unicos en todo el proyecto
            public const int $ARGUMENTS_YA_EXISTE = 11;
            public const int $ARGUMENTS_INACTIVO = 12;
        }
        public static class Argument
        {
            // Codigos para validaciones de argumentos (TArgumentException)
            // Enteros >= 11, unicos en todo el proyecto, independientes de Operation
            public const int FORMATO_INVALIDO_NOMBRE_$ARGUMENTS = 11;
        }
    }

    public static class Values
    {
        // Una constante por cada campo con restriccion de longitud
        public const int MAX_LENGTH_NOMBRE_$ARGUMENTS = 256;
        public const int MAX_LENGTH_DESCRIPCION_$ARGUMENTS = 512;
    }
}
```

**Reglas de ExceptionCodes:**
- `Operation` — para `TInvalidOperationException`: violaciones de reglas de negocio (estado, unicidad, flujo)
- `Argument` — para `TArgumentException` con codigo especifico: formato invalido de un campo concreto
- Ambas series empiezan en 11 de forma independiente entre si
- No repetir codigos dentro de la misma subcategoria en todo el proyecto

---

## Paso 2 — Validador de dominio

Crear `Domain/Validators/Entities/$ARGUMENTSValidator.cs`:

```csharp
using Bisoft.Exceptions;
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Domain.Validators.Entities;

public static class $ARGUMENTSValidator
{
    // Campo obligatorio: Trim() ANTES de validar, luego retornar valor formateado
    public static string FormatearNombre(this string? nombre)
    {
        nombre = nombre?.Trim();
        nombre.ValidateNull(StringValidator.ExceptionWhenNullArgument("nombre"))
              .ValidateLength(
                  minLength: 1,
                  maxLength: Values.MAX_LENGTH_NOMBRE_$ARGUMENTS,
                  exceptionWhenInvalid: StringValidator.ExceptionWhenOutOfRangeArguments("nombre", Values.MAX_LENGTH_NOMBRE_$ARGUMENTS)
              );
        return nombre!;
    }

    // Campo opcional con limite de longitud
    public static string? FormatearDescripcion(this string? descripcion)
    {
        descripcion = descripcion?.Trim();
        if (string.IsNullOrEmpty(descripcion)) return null;
        descripcion.ValidateLength(
            maxLength: Values.MAX_LENGTH_DESCRIPCION_$ARGUMENTS,
            exceptionWhenInvalid: StringValidator.ExceptionWhenOutOfRangeArguments("descripcion", Values.MAX_LENGTH_DESCRIPCION_$ARGUMENTS)
        );
        return descripcion;
    }

    // Con codigo de error especifico para TArgumentException
    public static string FormatearClave(this string? clave)
    {
        clave = clave?.Trim().ToUpper();
        var patron = @"^[A-Z0-9_]{3,20}$";
        clave.ValidateNull(StringValidator.ExceptionWhenNullArgument("clave"))
             .ValidateRegex(
                 patron,
                 new TArgumentException(
                     ExceptionCodes.Argument.FORMATO_INVALIDO_NOMBRE_$ARGUMENTS,
                     "La clave debe tener entre 3 y 20 caracteres alfanumericos en mayusculas"
                 )
             );
        return clave!;
    }

    // Campo email
    public static string FormatearEmail(this string? email)
    {
        email = email?.Trim().ToLower();
        email.ValidateNull(StringValidator.ExceptionWhenNullArgument("email"))
             .ValidateEmail(TArgumentException.InvalidFormat("El email no tiene un formato valido"));
        return email!;
    }
}
```

**Orden en `Formatear*()`:**
1. Aplicar `Trim()` (y/o cambio de casing) al inicio — **antes** de validar
2. Ejecutar validaciones encadenadas con `StringValidator`
3. Retornar el valor ya formateado

**Metodos disponibles en `StringValidator`:**
- `.ValidateNull(excepcion)` — lanza si nulo o vacio
- `.ValidateLength(min, max, excepcion)` — lanza si fuera de rango
- `.ValidateRegex(patron, excepcion)` — lanza si no coincide con el patron
- `.ValidateEmail(excepcion)` — valida formato de email

**Fabricas de excepcion en `Bisoft.Exceptions`:**

| Tipo | Factory / Constructor | Cuando usar |
|---|---|---|
| `TArgumentException.NullOrEmpty(msg)` | `StringValidator.ExceptionWhenNullArgument("campo")` | Campo nulo o vacio |
| `TArgumentException.OutOfRange(msg)` | `StringValidator.ExceptionWhenOutOfRangeArguments("campo", max)` | Longitud o rango invalido |
| `TArgumentException.InvalidFormat(msg)` | Directamente | Formato incorrecto (regex, email) |
| `new TArgumentException(code, msg)` | Con codigo de `ExceptionCodes.Argument` | Validacion con codigo especifico |
| `new TInvalidOperationException(code, msg, dict)` | Con codigo de `ExceptionCodes.Operation` | Regla de negocio violada |
| `TNotFoundException.EntityNotFound(msg)` | Directamente | Entidad no encontrada |

---

## Paso 3 — Entidad de dominio

Crear `Domain/Entities/{Modulo}/$ARGUMENTS.cs`:

```csharp
using Bisoft.Exceptions;
using {Namespace}.Domain.Entities.Base;
using {Namespace}.Domain.Validators.Entities;
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Domain.Entities.{Modulo};

public class $ARGUMENTS : Entity   // o : AggregateRootEntity si es hijo de un agregado
{
    // Propiedades inmutables — solo { get; }, asignadas en constructor, nunca cambian
    public string Nombre { get; }
    public DateTime FechaCreacionUtc { get; }   // sufijo Utc siempre en fechas UTC

    // Propiedades mutables — { get; private set; }, solo cambian via metodos de dominio
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaActualizacionUtc { get; private set; }

    // Referencia unica de navegacion — private set + default!
    // public OtraEntidad OtraEntidad { get; private set; } = default!;

    // Coleccion encapsulada — campo privado List<T> + propiedad IReadOnlyCollection<T>
    // private readonly List<EntidadHijo> _hijos = [];
    // public IReadOnlyCollection<EntidadHijo> Hijos => _hijos.AsReadOnly();

    // Constructor para EF Core — PRIVADO, sin parametros, sin logica
    private $ARGUMENTS() { }

    // Constructor de creacion — invoca Guards (Formatear*) para validaciones simples
    public $ARGUMENTS(string nombre, string? descripcion) : base()
    {
        Nombre = nombre.FormatearNombre();
        Descripcion = descripcion.FormatearDescripcion();
        FechaCreacionUtc = DateTime.UtcNow;
        FechaActualizacionUtc = FechaCreacionUtc;
        Activo = true;
    }

    // --- SOLO si hay validaciones de negocio complejas mas alla de Guards ---
    // El Domain Service consulta ANTES de llamar al Creator y pasa el resultado como parametro
    public static $ARGUMENTS Crear(string nombre, string? descripcion, bool yaExiste)
    {
        if (yaExiste)
            throw new TInvalidOperationException(
                ExceptionCodes.Operation.$ARGUMENTS_YA_EXISTE,
                "Ya existe un {Nombre} registrado con ese nombre",
                new Dictionary<string, object> { ["Nombre"] = nombre }
            );
        return new $ARGUMENTS(nombre, descripcion);
    }
    // ---

    // Metodos de dominio — un metodo por evento de negocio
    public void Actualizar(string nombre, string? descripcion)
    {
        Nombre = nombre.FormatearNombre();
        Descripcion = descripcion.FormatearDescripcion();
        FechaActualizacionUtc = DateTime.UtcNow;
    }

    public void Alternar() => Activo = !Activo;

    // Para colecciones (si aplica):
    // public void AgregarHijo(EntidadHijo hijo) => _hijos.Add(hijo);
    // public void QuitarHijo(EntidadHijo hijo) => _hijos.Remove(hijo);
}
```

**Reglas clave:**
- Constructor EF Core: `private`, sin parametros, sin logica de negocio
- Herencia `Entity` → genera su propio `Guid Id`; herencia `AggregateRootEntity` → `Id = Guid.Empty`, EF Core lo rellena
- Inmutable `{ get; }`: asignada en constructor, nunca cambia (Nombre, FechaCreacionUtc)
- Mutable `{ get; private set; }`: solo via metodos de dominio (Activo, FechaActualizacionUtc)
- Navegacion unica: `{ get; private set; } = default!;`
- Coleccion: `List<T>` privado + `IReadOnlyCollection<T>` → metodos `Agregar*` / `Quitar*`
- El Creator recibe resultados de consultas como argumentos — **nunca llama repositorios**

---

## Paso 4 — Mapping EF Core

Un archivo por proveedor en `Infrastructure/Mapping/{Modulo}/{Provider}/$ARGUMENTS{Provider}Configuration.cs`.

### SqlServer

```csharp
using Bisoft.DatabaseConnections.ColumnTypes;
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Infrastructure.Mapping.{Modulo}.SqlServer;

internal class $ARGUMENTSSqlServerConfiguration : IEntityTypeConfiguration<$ARGUMENTS>
{
    public void Configure(EntityTypeBuilder<$ARGUMENTS> builder)
    {
        builder.ToTable("{tabla_plural}", "{schema}")
            .HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Nombre)
            .HasColumnType(SqlServerColumnTypes.NVARCHAR)
            .HasColumnName("nombre")
            .HasMaxLength(Values.MAX_LENGTH_NOMBRE_$ARGUMENTS)
            .IsRequired();

        builder.Property(e => e.Activo)
            .HasColumnType(SqlServerColumnTypes.BIT)
            .HasColumnName("activo")
            .HasDefaultValue(true);

        builder.Property(e => e.FechaCreacionUtc)
            .HasColumnType(SqlServerColumnTypes.DATETIME2)
            .HasColumnName("fechaCreacionUtc")
            .IsRequired();

        builder.Property(e => e.FechaActualizacionUtc)
            .HasColumnType(SqlServerColumnTypes.DATETIME2)
            .HasColumnName("fechaActualizacionUtc")
            .IsRequired();

        // Relacion HasMany — para colecciones encapsuladas
        // builder.HasMany(e => e.Hijos)
        //     .WithOne(h => h.Padre)          // opcional si el hijo tiene referencia al padre
        //     .HasForeignKey(h => h.PadreId)
        //     .HasPrincipalKey(e => e.Id)
        //     .IsRequired()                   // false si la FK puede ser nula
        //     .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Postgres

```csharp
using static Bisoft.DatabaseConnections.ColumnTypes.PostgresColumnTypes;
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Infrastructure.Mapping.{Modulo}.Postgres;

internal class $ARGUMENTSPostgresConfiguration : IEntityTypeConfiguration<$ARGUMENTS>
{
    public void Configure(EntityTypeBuilder<$ARGUMENTS> builder)
    {
        builder.ToTable("{tabla_plural}", "{schema}")
            .HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType(UUID)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Nombre)
            .HasColumnType(VARCHAR)
            .HasColumnName("nombre")
            .HasMaxLength(Values.MAX_LENGTH_NOMBRE_$ARGUMENTS)
            .IsRequired();

        builder.Property(e => e.Activo)
            .HasColumnType(BOOLEAN)
            .HasColumnName("activo")
            .HasDefaultValue(true);

        builder.Property(e => e.FechaCreacionUtc)
            .HasColumnType(TIMESTAMPTZ)
            .HasColumnName("fecha_creacion_utc")
            .IsRequired();

        builder.Property(e => e.FechaActualizacionUtc)
            .HasColumnType(TIMESTAMPTZ)
            .HasColumnName("fecha_actualizacion_utc")
            .IsRequired();
    }
}
```

### Sqlite

```csharp
using static Bisoft.DatabaseConnections.ColumnTypes.SqliteColumnTypes;
using static {Namespace}.Domain.DomainConstants;

namespace {Namespace}.Infrastructure.Mapping.{Modulo}.Sqlite;

internal class $ARGUMENTSSqliteConfiguration : IEntityTypeConfiguration<$ARGUMENTS>
{
    public void Configure(EntityTypeBuilder<$ARGUMENTS> builder)
    {
        builder.ToTable("{tabla_plural}", "{schema}")
            .HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnType(TEXT)          // Guid como TEXT en Sqlite
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.Nombre)
            .HasColumnType(TEXT)          // sin HasMaxLength en TEXT de Sqlite
            .HasColumnName("nombre")
            .IsRequired();

        builder.Property(e => e.Activo)
            .HasColumnType(INTEGER)
            .HasColumnName("activo")
            .HasDefaultValue(1);

        builder.Property(e => e.FechaCreacionUtc)
            .HasColumnType(TEXT)          // DateTime como TEXT en Sqlite
            .HasColumnName("fecha_creacion_utc")
            .IsRequired();

        builder.Property(e => e.FechaActualizacionUtc)
            .HasColumnType(TEXT)
            .HasColumnName("fecha_actualizacion_utc")
            .IsRequired();
    }
}
```

### Entidad hijo de coleccion (AggregateRootEntity)

Cuando una entidad pertenece a una coleccion de otra, su PK puede no ser autogenerada. En ese caso agregar:

```csharp
builder.Property(e => e.Id)
    .HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER)
    .HasColumnName("id")
    .IsRequired()
    .ValueGeneratedOnAddNever();   // OBLIGATORIO si EF Core no genera el Id automaticamente
```

---

## Tipos de columna por proveedor (referencia completa)

### SqlServerColumnTypes

| Constante | SQL type | C# type |
|---|---|---|
| `UNIQUEIDENTIFIER` | uniqueidentifier | `Guid` |
| `NVARCHAR` | nvarchar | `string` (requiere `HasMaxLength`) |
| `VARCHAR` | varchar | `string` (requiere `HasMaxLength`) |
| `CHAR` | char | `char` / `string` |
| `INT` | int | `int` |
| `SMALLINT` | smallint | `short` |
| `BIGINT` | bigint | `long` |
| `BIT` | bit | `bool` |
| `DECIMAL` | decimal | `decimal` (usar `HasPrecision`) |
| `MONEY` | money | `decimal` |
| `FLOAT` | float | `double` |
| `DATETIME2` | datetime2 | `DateTime` |
| `DATE` | date | `DateOnly` / `DateTime` |
| `TIME` | time | `TimeSpan` |

### PostgresColumnTypes

| Constante | SQL type | C# type |
|---|---|---|
| `UUID` | uuid | `Guid` |
| `VARCHAR` | varchar | `string` (requiere `HasMaxLength`) |
| `TEXT` | text | `string` (sin `HasMaxLength`) |
| `CHAR` | char | `char` / `string` |
| `INTEGER` | integer | `int` |
| `SMALLINT` | smallint | `short` |
| `BIGINT` | bigint | `long` |
| `BOOLEAN` | boolean | `bool` |
| `NUMERIC` | numeric | `decimal` (usar `HasPrecision`) |
| `MONEY` | money | `decimal` |
| `REAL` | real | `float` |
| `DOUBLE_PRECISION` | double precision | `double` |
| `TIMESTAMPTZ` | timestamptz | `DateTime` UTC |
| `DATE` | date | `DateOnly` / `DateTime` |
| `TIME` | time | `TimeSpan` |

### SqliteColumnTypes

| Constante | SQL type | C# types |
|---|---|---|
| `TEXT` | TEXT | `string`, `Guid`, `DateTime` |
| `INTEGER` | INTEGER | `int`, `long`, `short`, `bool` |
| `REAL` | REAL | `float`, `double` |
| `NUMERIC` | NUMERIC | `decimal` |
| `BLOB` | BLOB | `byte[]` |

> **Sqlite**: `Guid` y `DateTime` van como `TEXT`. No usar `HasMaxLength` en columnas `TEXT`.

---

## Paso 5 — Interfaz de repositorio

Crear `Domain/Repositories/I$ARGUMENTSRepository.cs`:

```csharp
using Bisoft.DatabaseConnections.Util.Abstractions;

namespace {Namespace}.Domain.Repositories;

public interface I$ARGUMENTSRepository : IEFRepository
{
    IQueryable<$ARGUMENTS> Consultar$ARGUMENTSs();
    Task<List<$ARGUMENTS>> Obtener$ARGUMENTSs(IQueryable<$ARGUMENTS> query, CancellationToken ct = default);
    Task<$ARGUMENTS?> Obtener$ARGUMENTS(IOrderedQueryable<$ARGUMENTS> query, CancellationToken ct = default);
    Task<$ARGUMENTS?> ObtenerPorId(Guid id, CancellationToken ct = default);
    Task Crear($ARGUMENTS entidad, CancellationToken ct = default);
    // Metodos adicionales especificos si aplican
}
```

> La interfaz hereda de `IEFRepository` (`Bisoft.DatabaseConnections.Util.Abstractions`), que expone `SaveChanges(Dictionary<string, string>?, CancellationToken)`.

---

## Paso 6 — Implementacion de repositorio

Crear `Infrastructure/Repositories/{Modulo}/$ARGUMENTSRepository.cs`:

```csharp
using Bisoft.DatabaseConnections.Repositories;
using Bisoft.Logging.Util;
using {Namespace}.Domain.Entities.{Modulo};
using {Namespace}.Domain.Repositories;
using {Namespace}.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Repositories.{Modulo};

public class $ARGUMENTSRepository : EFRepository<{Modulo}Context>, I$ARGUMENTSRepository
{
    public $ARGUMENTSRepository({Modulo}Context context, LoggerWrapper<$ARGUMENTSRepository> logger)
        : base(context, logger) { }

    public IQueryable<$ARGUMENTS> Consultar$ARGUMENTSs()
    {
        _logger.LogDebug("Consultando $ARGUMENTS");
        return _context.$ARGUMENTSs;
    }

    public async Task<List<$ARGUMENTS>> Obtener$ARGUMENTSs(IQueryable<$ARGUMENTS> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo lista de $ARGUMENTS");
        return await query.ToListAsync(ct);
    }

    public async Task<$ARGUMENTS?> Obtener$ARGUMENTS(IOrderedQueryable<$ARGUMENTS> query, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo $ARGUMENTS");
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<$ARGUMENTS?> ObtenerPorId(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Obteniendo $ARGUMENTS {Id}", id);
        return await _context.$ARGUMENTSs
            .Where(e => e.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task Crear($ARGUMENTS entidad, CancellationToken ct = default)
    {
        _logger.LogDebug("Creando $ARGUMENTS {Id}", entidad.Id);
        await _context.$ARGUMENTSs.AddAsync(entidad, ct);
    }
}
```

> El repositorio hereda `EFRepository<TContext>` que expone `_context`, `_logger` y `SaveChanges(metadata, ct)`.
> Usar `LoggerWrapper<TRepo>` en el constructor (el tipo generico), que la base recibe como `LoggerWrapper`.

---

## Paso 7 — Registro DI

Indicar al desarrollador que agregue en `Api/Extensions/ServiceExtensions.cs` dentro de `ConfigureServices`:

```csharp
services.AddScoped<I$ARGUMENTSRepository, $ARGUMENTSRepository>();
```

Y en el DbContext correspondiente:

```csharp
public DbSet<$ARGUMENTS> $ARGUMENTSs { get; set; }
```
