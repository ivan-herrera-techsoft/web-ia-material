---
description: Convenciones para EF Core mapping — IEntityTypeConfiguration por proveedor con ColumnTypes y convenciones de columna
globs: "**/Mapping/**,**/Infrastructure/**"
---

## Estructura de archivos de mapping

Un archivo de configuración por entidad **y por proveedor** en `Infrastructure/Mapping/{Modulo}/{Provider}/`:

```
Infrastructure/Mapping/Canales/
├── SqlServer/
│   └── CanalSqlServerConfiguration.cs
├── Postgres/
│   └── CanalPostgresConfiguration.cs
└── Sqlite/
    └── CanalSqliteConfiguration.cs
```

Cada clase implementa `IEntityTypeConfiguration<T>` y es `internal`.

---

## Convención de nombres de columna por proveedor

| Proveedor | Convención | Ejemplo (propiedad `FechaCreacionUtc`) |
|---|---|---|
| SqlServer | `camelCase` | `fechaCreacionUtc` |
| Postgres | `snake_case` | `fecha_creacion_utc` |
| Sqlite | `snake_case` | `fecha_creacion_utc` |

Aplicar explícitamente en cada columna con `.HasColumnName()`.

---

## Tipos de columna — usar constantes del paquete

Usar siempre las clases estáticas de `Bisoft.DatabaseConnections.ColumnTypes`. **Nunca strings literales.**

```csharp
using Bisoft.DatabaseConnections.ColumnTypes;

// SqlServer
builder.Property(e => e.Nombre)
    .HasColumnType(SqlServerColumnTypes.NVARCHAR)
    .HasMaxLength(DomainConstants.Values.MAX_LENGTH_NOMBRE_CANAL)
    .HasColumnName("nombre");

// Postgres
builder.Property(e => e.Nombre)
    .HasColumnType(PostgresColumnTypes.VARCHAR)
    .HasMaxLength(DomainConstants.Values.MAX_LENGTH_NOMBRE_CANAL)
    .HasColumnName("nombre");

// Sqlite — TEXT, sin HasMaxLength
builder.Property(e => e.Nombre)
    .HasColumnType(SqliteColumnTypes.TEXT)
    .HasColumnName("nombre");
```

**Regla Sqlite**: `Guid` y `DateTime` se mapean como `TEXT`. Nunca usar `HasMaxLength` sobre columnas `TEXT` en Sqlite.

---

## Tabla de tipos CLR → SQL

| CLR | SqlServer | Postgres | Sqlite |
|---|---|---|---|
| `string` | `NVARCHAR` + `HasMaxLength` | `VARCHAR` + `HasMaxLength` | `TEXT` |
| `Guid` | `UNIQUEIDENTIFIER` | `UUID` | `TEXT` |
| `bool` | `BIT` | `BOOLEAN` | `INTEGER` |
| `DateTime` | `DATETIME2` | `TIMESTAMPTZ` | `TEXT` |
| `int` | `INT` | `INTEGER` | `INTEGER` |
| `decimal` | `DECIMAL` + `HasPrecision` | `NUMERIC` + `HasPrecision` | `REAL` |
| `long` | `BIGINT` | `BIGINT` | `INTEGER` |

---

## Estructura de configuración SqlServer (ejemplo completo)

```csharp
namespace Company.Product.Module.Infrastructure.Mapping.Canales.SqlServer;

internal class CanalSqlServerConfiguration : IEntityTypeConfiguration<Canal>
{
    public void Configure(EntityTypeBuilder<Canal> builder)
    {
        builder.ToTable("canal", "cnl");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER)
            .HasColumnName("id");

        builder.Property(e => e.Nombre)
            .HasColumnType(SqlServerColumnTypes.NVARCHAR)
            .HasMaxLength(DomainConstants.Values.MAX_LENGTH_NOMBRE_CANAL)
            .HasColumnName("nombre")
            .IsRequired();

        builder.Property(e => e.Activo)
            .HasColumnType(SqlServerColumnTypes.BIT)
            .HasColumnName("activo");

        builder.Property(e => e.FechaCreacionUtc)
            .HasColumnType(SqlServerColumnTypes.DATETIME2)
            .HasColumnName("fechaCreacionUtc");
    }
}
```

---

## Relaciones y colecciones

```csharp
// Uno a muchos (padre → hijos)
builder.HasMany(e => e.Versiones)
       .WithOne(v => v.Canal)
       .HasForeignKey(v => v.CanalId)
       .OnDelete(DeleteBehavior.Cascade);
```

---

## Entidades hijo — sin autogeneración de ID

Las entidades que heredan de `AggregateRootEntity` (hijas del agregado) tienen su `Id` asignado por la aplicación. Declarar `ValueGeneratedOnAddNever()` para evitar que la BD intente generarlo:

```csharp
builder.Property(e => e.Id)
    .HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER)
    .HasColumnName("id")
    .ValueGeneratedOnAddNever();
```

---

## Registro en Strategies

Cada archivo de configuración se registra en la Strategy correspondiente (ver `rules/context.md`). Las entidades padre SIEMPRE antes que las hijas (dependencia de FK):

```csharp
// En CanalContextSqlServerConfigurationStrategy
modelBuilder.ApplyConfiguration(new CanalSqlServerConfiguration());      // padre
modelBuilder.ApplyConfiguration(new SubcanalSqlServerConfiguration());   // hija
```
