# Spec: DbContext de Infraestructura

## Propósito

Define las reglas contractuales para crear la infraestructura de base de datos de un módulo: DbContext, Factory, Strategies por proveedor y su registro en el contenedor de DI.

> **Spec relacionado:** Los archivos de mapping aplicados en las Strategies de esta spec se generan como parte de la creación de entidades. Ver → [Spec: Entidades de Dominio](crear-entidad.md) — SC-ENT-08.

---

## Contratos obligatorios

### SC-CTX-01: DbContext hereda de ConfiguredDbContext

El `DbContext` DEBE heredar de `ConfiguredDbContext<TFactory>` del paquete `Bisoft.DatabaseConnections.Contexts`. **Nunca** heredar de `DbContext` directamente ni sobreescribir `OnModelCreating`.

```csharp
public class CanalContext : ConfiguredDbContext<CanalDbManagerStrategyFactory>
{
    public DbSet<Canal> Canales { get; set; }
    public CanalContext(DbContextOptions<CanalContext> options) : base(options) { }
}
```

El tipo generico de `ConfiguredDbContext<TFactory>` DEBE ser la Factory del mismo módulo.

---

### SC-CTX-02: Un contexto por módulo

Cada módulo de negocio tiene su propio `DbContext`. No se comparte el mismo contexto entre módulos distintos. El nombre sigue el patrón `{Modulo}Context`.

---

### SC-CTX-03: Factory con claves de proveedor como constantes

La Factory DEBE heredar de `ModelBuilderConfigurationsFactory` y registrar las Strategies usando las constantes de `DatabaseConnections.DatabaseProviders` — nunca strings literales.

```csharp
public class CanalDbManagerStrategyFactory : ModelBuilderConfigurationsFactory
{
    protected override Dictionary<string, IModelBuilderConfigurations> SupportedDatabasesConfigurations => new()
    {
        { DatabaseConnections.DatabaseProviders.SQLSERVER, new CanalContextSqlServerConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.POSTGRES,  new CanalContextPostgresConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.SQLITE,    new CanalContextSqliteConfigurationStrategy() }
    };
}
```

La Factory es `public`. Las Strategies son `internal`.

---

### SC-CTX-04: Strategies aplican configuraciones de entidades en orden

Cada Strategy implementa `IModelBuilderConfigurations` y aplica los `IEntityTypeConfiguration<T>` en orden: entidades padre antes que entidades hijo (por dependencia de FK).

```csharp
internal class CanalContextSqlServerConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CanalSqlServerConfiguration());
        // modelBuilder.ApplyConfiguration(new SubcanalSqlServerConfiguration());
        return modelBuilder;
    }
}
```

---

### SC-CTX-05: Convención de nombres de columna por proveedor

| Proveedor | Convención | Ejemplo propiedad `FechaCreacionUtc` |
|---|---|---|
| SqlServer | `camelCase` | `fechaCreacionUtc` |
| Postgres | `snake_case` | `fecha_creacion_utc` |
| Sqlite | `snake_case` | `fecha_creacion_utc` |

La convención se aplica en cada archivo de mapping de entidad — no en la Strategy.

---

### SC-CTX-06: Tipos de columna desde constantes del paquete

Los tipos de columna SQL DEBEN usarse desde las clases estáticas del paquete `Bisoft.DatabaseConnections.ColumnTypes`. Nunca strings literales de tipo.

| Campo CLR | SqlServer | Postgres | Sqlite |
|---|---|---|---|
| `Guid` | `SqlServerColumnTypes.UNIQUEIDENTIFIER` | `PostgresColumnTypes.UUID` | `TEXT` (sin HasMaxLength) |
| `string` | `SqlServerColumnTypes.NVARCHAR` + `HasMaxLength` | `PostgresColumnTypes.VARCHAR` + `HasMaxLength` | `TEXT` (sin HasMaxLength) |
| `bool` | `SqlServerColumnTypes.BIT` | `PostgresColumnTypes.BOOLEAN` | `SqliteColumnTypes.INTEGER` |
| `DateTime` | `SqlServerColumnTypes.DATETIME2` | `PostgresColumnTypes.TIMESTAMPTZ` | `TEXT` |
| `int` | `SqlServerColumnTypes.INT` | `PostgresColumnTypes.INTEGER` | `SqliteColumnTypes.INTEGER` |
| `decimal` | `SqlServerColumnTypes.DECIMAL` + `HasPrecision` | `PostgresColumnTypes.NUMERIC` + `HasPrecision` | `SqliteColumnTypes.REAL` |

En Sqlite, `Guid` y `DateTime` se mapean como `TEXT` — **nunca** usar `HasMaxLength` en columnas `TEXT` de Sqlite.

---

### SC-CTX-07: Registro en ConfigureContexts via factory de opciones

El `DbContext` DEBE registrarse en `ConfigureContexts()` usando `DbContextOptionsBuilderStrategyFactory.Create(connectionConfig)`. **Nunca** llamar a `UseSqlServer`, `UseNpgsql` o `UseSqlite` directamente.

```csharp
public static IServiceCollection ConfigureContexts(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    var builderStrategy = DbContextOptionsBuilderStrategyFactory.Create(configuration.CanalConnection);
    services.AddDbContext<CanalContext>(builderStrategy.GetBuilder());
    return services;
}
```

---

### SC-CTX-08: Propiedad de conexión en GeneralConfiguration

La conexión del contexto DEBE estar tipada como `DbConnectionConfiguration` en `GeneralConfiguration` y leerse desde el `ConfigurationsReader` correspondiente. La clave en `appsettings.json` sigue el patrón `{NombreConexion}`.

```json
{
  "CanalConnection": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=...;Database=...;"
  }
}
```

Proveedores válidos: `"SqlServer"`, `"Postgres"`, `"Sqlite"`.

---

### SC-CTX-09: Entidades hijo sin autogeneración de ID

Las entidades hijo de agregado (que heredan de `AggregateRootEntity`) tienen su `Id` asignado por EF Core. El mapping DEBE indicar `ValueGeneratedOnAddNever()` para desactivar la autogeneración en la BD.

```csharp
builder.Property(e => e.Id).ValueGeneratedOnAddNever();
```

---

### SC-CTX-10: Estructura de directorios obligatoria

```
Infrastructure/
├── Contexts/
│   └── {Modulo}Context.cs
├── Helpers/
│   ├── Factories/
│   │   └── {Modulo}DbManagerStrategyFactory.cs
│   └── Strategies/
│       └── {Modulo}/
│           └── DbConfigurations/
│               ├── {Modulo}ContextSqlServerConfigurationStrategy.cs
│               ├── {Modulo}ContextPostgresConfigurationStrategy.cs
│               └── {Modulo}ContextSqliteConfigurationStrategy.cs
└── Mapping/
    └── {Modulo}/
        ├── SqlServer/
        │   └── {Entidad}SqlServerConfiguration.cs
        ├── Postgres/
        │   └── {Entidad}PostgresConfiguration.cs
        └── Sqlite/
            └── {Entidad}SqliteConfiguration.cs
```
