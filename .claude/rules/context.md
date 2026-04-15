---
description: Convenciones para DbContext, Factories y Strategies de configuracion de base de datos
globs: "**/Contexts/**,**/Helpers/Factories/**,**/Helpers/Strategies/**"
---

## DbContext

### Ubicacion y herencia

- Ubicacion: `Infrastructure/Contexts/{Modulo}Context.cs`
- Hereda de `ConfiguredDbContext<TFactory>` del paquete `Bisoft.DatabaseConnections.Contexts`
- El parametro generico `TFactory` es la Factory especifica del modulo

```csharp
using Bisoft.DatabaseConnections.Contexts;

namespace Empresa.Producto.Modulo.Infrastructure.Contexts;

public class CanalContext : ConfiguredDbContext<CanalDbManagerStrategyFactory>
{
    public DbSet<Canal> Canales { get; set; }
    public DbSet<TipoEvento> TiposEvento { get; set; }

    public CanalContext(DbContextOptions<CanalContext> options) : base(options) { }
}
```

**Reglas:**
- Un DbContext por modulo/bounded context (no un contexto global para todo el proyecto)
- Constructor con `DbContextOptions<TSelf>` pasado a `base(options)` — sin otro codigo en el constructor
- Una propiedad `DbSet<T>` por entidad que participa en el contexto
- No configurar mappings directamente en `OnModelCreating`; la base lo delega a la Factory

---

## Factory

### Ubicacion y herencia

- Ubicacion: `Infrastructure/Helpers/Factories/{Modulo}DbManagerStrategyFactory.cs`
- Hereda de `ModelBuilderConfigurationsFactory` del paquete `Bisoft.DatabaseConnections.Factories`
- Clase publica (el DbContext la referencia via generico)

### Estructura

```csharp
using Bisoft.DatabaseConnections.Factories;
using Bisoft.DatabaseConnections.Strategies;
using Empresa.Producto.Modulo.Infrastructure.Helpers.Strategies.Canales.DbConfigurations;

namespace Empresa.Producto.Modulo.Infrastructure.Helpers.Factories;

public class CanalDbManagerStrategyFactory : ModelBuilderConfigurationsFactory
{
    protected override Dictionary<string, IModelBuilderConfigurations> SupportedDatabasesConfigurations => new()
    {
        { DatabaseConnections.DatabaseProviders.SQLSERVER, new CanalContextSqlServerConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.SQLITE,    new CanalContextSqliteConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.POSTGRES,  new CanalContextPostgresConfigurationStrategy() }
    };
}
```

**Reglas:**
- Override de `SupportedDatabasesConfigurations` con una entrada por proveedor soportado
- Las claves son las constantes de `DatabaseConnections.DatabaseProviders` — nunca strings literales
- El valor es una instancia de la Strategy correspondiente al proveedor

### Proveedores disponibles

| Constante | Proveedor |
|---|---|
| `DatabaseConnections.DatabaseProviders.SQLSERVER` | SQL Server |
| `DatabaseConnections.DatabaseProviders.POSTGRES` | PostgreSQL |
| `DatabaseConnections.DatabaseProviders.SQLITE` | SQLite |

---

## Strategies

### Ubicacion y herencia

- Ubicacion: `Infrastructure/Helpers/Strategies/{Modulo}/DbConfigurations/{Modulo}Context{Provider}ConfigurationStrategy.cs`
- Implementan `IModelBuilderConfigurations` del paquete `Bisoft.DatabaseConnections.Strategies`
- Clase `internal` (solo accedida desde la Factory del mismo proyecto)

### Estructura por proveedor

**SqlServer:**
```csharp
using Bisoft.DatabaseConnections.Strategies;
using Empresa.Producto.Modulo.Infrastructure.Mapping.Canales.SqlServer;

namespace Empresa.Producto.Modulo.Infrastructure.Helpers.Strategies.Canales.DbConfigurations;

internal class CanalContextSqlServerConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CanalSqlServerConfiguration());
        modelBuilder.ApplyConfiguration(new TipoEventoSqlServerConfiguration());
        return modelBuilder;
    }
}
```

**Postgres:**
```csharp
using Bisoft.DatabaseConnections.Strategies;
using Empresa.Producto.Modulo.Infrastructure.Mapping.Canales.Postgres;

namespace Empresa.Producto.Modulo.Infrastructure.Helpers.Strategies.Canales.DbConfigurations;

internal class CanalContextPostgresConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CanalPostgresConfiguration());
        modelBuilder.ApplyConfiguration(new TipoEventoPostgresConfiguration());
        return modelBuilder;
    }
}
```

**Sqlite:**
```csharp
using Bisoft.DatabaseConnections.Strategies;
using Empresa.Producto.Modulo.Infrastructure.Mapping.Canales.Sqlite;

namespace Empresa.Producto.Modulo.Infrastructure.Helpers.Strategies.Canales.DbConfigurations;

internal class CanalContextSqliteConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CanalSqliteConfiguration());
        modelBuilder.ApplyConfiguration(new TipoEventoSqliteConfiguration());
        return modelBuilder;
    }
}
```

**Reglas:**
- Metodo `ApplyConfigurations` llama `modelBuilder.ApplyConfiguration(new {Entidad}{Provider}Configuration())` por cada entidad del contexto
- El orden de `ApplyConfiguration` debe respetar dependencias: entidades padre antes que hijas
- Retornar el `modelBuilder` al final

---

## Tipos de columna por proveedor

### SqlServer (`Bisoft.DatabaseConnections.ColumnTypes.SqlServerColumnTypes`)

| Constante | Tipo SQL |
|---|---|
| `UNIQUEIDENTIFIER` | uniqueidentifier (Guid) |
| `NVARCHAR` | nvarchar (requiere `HasMaxLength`) |
| `INT` | int |
| `BIT` | bit (bool) |
| `DATETIME2` | datetime2 |
| `DECIMAL` | decimal (usar `HasPrecision`) |
| `BIGINT` | bigint |

```csharp
using Bisoft.DatabaseConnections.ColumnTypes;
// o: using static Bisoft.DatabaseConnections.ColumnTypes.SqlServerColumnTypes;

builder.Property(e => e.Id).HasColumnType(SqlServerColumnTypes.UNIQUEIDENTIFIER);
```

### Postgres (`Bisoft.DatabaseConnections.ColumnTypes.PostgresColumnTypes`)

| Constante | Tipo SQL |
|---|---|
| `UUID` | uuid (Guid) |
| `VARCHAR` | varchar (requiere `HasMaxLength`) |
| `INTEGER` | integer |
| `BOOLEAN` | boolean |
| `TIMESTAMPTZ` | timestamptz (DateTime UTC) |
| `NUMERIC` | numeric |
| `BIGINT` | bigint |

```csharp
using static Bisoft.DatabaseConnections.ColumnTypes.PostgresColumnTypes;

builder.Property(e => e.Id).HasColumnType(UUID);
builder.Property(e => e.FechaCreacion).HasColumnType(TIMESTAMPTZ);
```

### Sqlite (`Bisoft.DatabaseConnections.ColumnTypes.SqliteColumnTypes`)

| Constante | Tipo SQL |
|---|---|
| `TEXT` | TEXT (Guid, string, DateTime) |
| `INTEGER` | INTEGER |
| `REAL` | REAL |
| `BLOB` | BLOB |

```csharp
using static Bisoft.DatabaseConnections.ColumnTypes.SqliteColumnTypes;

builder.Property(e => e.Id).HasColumnType(TEXT);
builder.Property(e => e.FechaCreacion).HasColumnType(TEXT);
```

> Sqlite mapea Guid y DateTime como `TEXT`. No usar `HasMaxLength` en columnas TEXT de Sqlite.

---

## Convencion de nombres de columna por proveedor

| Proveedor | Convension | Ejemplo |
|---|---|---|
| SqlServer | `camelCase` | `fechaCreacion`, `nombreUsuario` |
| Postgres | `snake_case` | `fecha_creacion`, `nombre_usuario` |
| Sqlite | `snake_case` | `fecha_creacion`, `nombre_usuario` |

---

## Registro en ServiceExtensions

El contexto se registra en `Api/Extensions/ServiceExtensions.cs` dentro del metodo `ConfigureContexts`:

```csharp
public static IServiceCollection ConfigureContexts(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddDbContext<CanalContext>(configuration.CanalConnection);
    // Agregar un AddDbContext<T> por cada contexto del proyecto
    return services;
}

// Metodo privado reutilizable — no duplicar por cada contexto
private static IServiceCollection AddDbContext<TContext>(
    this IServiceCollection services,
    DbConnectionConfiguration connectionConfiguration)
    where TContext : DbContext
{
    var builderStrategy = DbContextOptionsBuilderStrategyFactory.Create(connectionConfiguration);
    services.AddDbContext<TContext>(builderStrategy.GetBuilder());
    return services;
}
```

- `DbConnectionConfiguration` proviene de `GeneralConfiguration` mapeada desde `appsettings.json`
- `DbContextOptionsBuilderStrategyFactory.Create()` selecciona el proveedor correcto segun la configuracion en tiempo de ejecucion
- La conexion de cada contexto es una propiedad separada en `GeneralConfiguration`

---

## Flujo completo de la arquitectura

```
appsettings.json (SecurityConnection: { Provider: "SqlServer", ConnectionString: "..." })
        ↓
GeneralConfiguration.SecurityConnection : DbConnectionConfiguration
        ↓
ConfigureContexts() → AddDbContext<SecurityContext>(configuration.SecurityConnection)
        ↓
DbContextOptionsBuilderStrategyFactory.Create(connectionConfiguration)
        → selecciona el builder de EF Core correcto (UseSqlServer / UseNpgsql / UseSqlite)
        ↓
SecurityContext : ConfiguredDbContext<SecurityDbManagerStrategyFactory>
        → en OnModelCreating llama a SecurityDbManagerStrategyFactory
        ↓
SecurityDbManagerStrategyFactory : ModelBuilderConfigurationsFactory
        → busca la Strategy segun el proveedor activo
        ↓
ContextSqlServerConfigurationStrategy : IModelBuilderConfigurations
        → aplica UsuarioSqlServerConfiguration, etc.
```
