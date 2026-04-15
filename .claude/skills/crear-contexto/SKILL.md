---
name: crear-contexto
description: Scaffolda un DbContext de infraestructura completo — Context, Factory, Strategies por proveedor y registro en ServiceExtensions
argument-hint: <NombreModulo en español> ej. "Canal", "Seguridad", "Facturacion"
---

Crear la infraestructura de base de datos para el modulo **$ARGUMENTS**.

Antes de generar los archivos, **preguntar al desarrollador**:
1. ¿Que entidades pertenecen a este contexto? (lista separada por comas)
2. ¿Que proveedores de BD soporta el proyecto? (SqlServer, Postgres, Sqlite o combinacion)
3. ¿Como se llama la propiedad de conexion en `GeneralConfiguration`? (ej. `CanalConnection`)
4. ¿Cual es el schema de BD para este modulo? (ej. `cnl`, `seg`, `fac`)

---

## Paso 1 — DbContext

Crear `Infrastructure/Contexts/$ARGUMENTSContext.cs`:

```csharp
using Bisoft.DatabaseConnections.Contexts;
using {Namespace}.Domain.Entities.{Modulo};
using {Namespace}.Infrastructure.Helpers.Factories;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Contexts;

public class $ARGUMENTSContext : ConfiguredDbContext<$ARGUMENTSDbManagerStrategyFactory>
{
    // Un DbSet<T> por entidad que participa en el contexto
    public DbSet<{Entidad1}> {Entidad1}s { get; set; }
    // public DbSet<{Entidad2}> {Entidad2}s { get; set; }

    public $ARGUMENTSContext(DbContextOptions<$ARGUMENTSContext> options) : base(options) { }
}
```

**Reglas:**
- Hereda de `ConfiguredDbContext<TFactory>` — no sobreescribir `OnModelCreating`
- El tipo generico es la Factory del mismo modulo
- Constructor pasa `options` a `base(options)` sin logica adicional

---

## Paso 2 — Factory

Crear `Infrastructure/Helpers/Factories/$ARGUMENTSDbManagerStrategyFactory.cs`:

```csharp
using Bisoft.DatabaseConnections;
using Bisoft.DatabaseConnections.Factories;
using Bisoft.DatabaseConnections.Strategies;
using {Namespace}.Infrastructure.Helpers.Strategies.$ARGUMENTS.DbConfigurations;

namespace {Namespace}.Infrastructure.Helpers.Factories;

public class $ARGUMENTSDbManagerStrategyFactory : ModelBuilderConfigurationsFactory
{
    protected override Dictionary<string, IModelBuilderConfigurations> SupportedDatabasesConfigurations => new()
    {
        { DatabaseConnections.DatabaseProviders.SQLSERVER, new $ARGUMENTSContextSqlServerConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.SQLITE,    new $ARGUMENTSContextSqliteConfigurationStrategy() },
        { DatabaseConnections.DatabaseProviders.POSTGRES,  new $ARGUMENTSContextPostgresConfigurationStrategy() }
    };
}
```

**Reglas:**
- Hereda de `ModelBuilderConfigurationsFactory`
- Clave = constante de `DatabaseConnections.DatabaseProviders` (nunca string literal)
- Incluir SOLO los proveedores que el proyecto soporta
- Si el proyecto usa un solo proveedor, igualmente registrar los demas como buena practica

---

## Paso 3 — Strategies

### SqlServer

Crear `Infrastructure/Helpers/Strategies/$ARGUMENTS/DbConfigurations/$ARGUMENTSContextSqlServerConfigurationStrategy.cs`:

```csharp
using Bisoft.DatabaseConnections.Strategies;
using {Namespace}.Infrastructure.Mapping.{Modulo}.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Helpers.Strategies.$ARGUMENTS.DbConfigurations;

internal class $ARGUMENTSContextSqlServerConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        // Aplicar en orden: entidades padre antes que hijas
        modelBuilder.ApplyConfiguration(new {Entidad1}SqlServerConfiguration());
        // modelBuilder.ApplyConfiguration(new {Entidad2}SqlServerConfiguration());
        return modelBuilder;
    }
}
```

### Postgres

Crear `Infrastructure/Helpers/Strategies/$ARGUMENTS/DbConfigurations/$ARGUMENTSContextPostgresConfigurationStrategy.cs`:

```csharp
using Bisoft.DatabaseConnections.Strategies;
using {Namespace}.Infrastructure.Mapping.{Modulo}.Postgres;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Helpers.Strategies.$ARGUMENTS.DbConfigurations;

internal class $ARGUMENTSContextPostgresConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new {Entidad1}PostgresConfiguration());
        return modelBuilder;
    }
}
```

### Sqlite

Crear `Infrastructure/Helpers/Strategies/$ARGUMENTS/DbConfigurations/$ARGUMENTSContextSqliteConfigurationStrategy.cs`:

```csharp
using Bisoft.DatabaseConnections.Strategies;
using {Namespace}.Infrastructure.Mapping.{Modulo}.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace {Namespace}.Infrastructure.Helpers.Strategies.$ARGUMENTS.DbConfigurations;

internal class $ARGUMENTSContextSqliteConfigurationStrategy : IModelBuilderConfigurations
{
    public ModelBuilder ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new {Entidad1}SqliteConfiguration());
        return modelBuilder;
    }
}
```

---

## Paso 4 — Mapping de entidades

Por cada entidad y proveedor, crear un archivo de configuracion en `Infrastructure/Mapping/{Modulo}/{Provider}/`.
Ver el skill `crear-entidad` para el detalle de cada archivo de mapping.

**Referencia rapida de tipos de columna:**

| Campo | SqlServer | Postgres | Sqlite |
|---|---|---|---|
| Guid (Id) | `UNIQUEIDENTIFIER` | `UUID` | `TEXT` |
| string | `NVARCHAR` + `HasMaxLength` | `VARCHAR` + `HasMaxLength` | `TEXT` |
| bool | `BIT` | `BOOLEAN` | `INTEGER` |
| DateTime UTC | `DATETIME2` | `TIMESTAMPTZ` | `TEXT` |
| int | `INT` | `INTEGER` | `INTEGER` |
| decimal | `DECIMAL` + `HasPrecision` | `NUMERIC` + `HasPrecision` | `REAL` |

**Convencion de nombres de columna:**

| Proveedor | Convension | Ejemplo |
|---|---|---|
| SqlServer | `camelCase` | `fechaCreacion` |
| Postgres | `snake_case` | `fecha_creacion` |
| Sqlite | `snake_case` | `fecha_creacion` |

---

## Paso 5 — Registro en ServiceExtensions

Indicar al desarrollador que en `Api/Extensions/ServiceExtensions.cs`:

### 5a. Agregar metodo `ConfigureContexts` (o extender el existente)

```csharp
public static IServiceCollection ConfigureContexts(
    this IServiceCollection services,
    GeneralConfiguration configuration)
{
    services.AddDbContext<$ARGUMENTSContext>(configuration.{NombreConexion});
    return services;
}

// Metodo privado compartido (agregar solo si no existe ya)
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

### 5b. Agregar la conexion en `GeneralConfiguration`

```csharp
public class GeneralConfiguration
{
    // Agregar una propiedad por cada contexto
    public DbConnectionConfiguration {NombreConexion} { get; set; }
}
```

### 5c. Agregar en `appsettings.json`

```json
{
  "{NombreConexion}": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=...;Database=...;Trusted_Connection=True;"
  }
}
```

Proveedores validos en `appsettings.json`: `"SqlServer"`, `"Postgres"`, `"Sqlite"`.

---

## Resumen de archivos generados

```
Infrastructure/
├── Contexts/
│   └── $ARGUMENTSContext.cs
├── Helpers/
│   ├── Factories/
│   │   └── $ARGUMENTSDbManagerStrategyFactory.cs
│   └── Strategies/
│       └── $ARGUMENTS/
│           └── DbConfigurations/
│               ├── $ARGUMENTSContextSqlServerConfigurationStrategy.cs
│               ├── $ARGUMENTSContextPostgresConfigurationStrategy.cs
│               └── $ARGUMENTSContextSqliteConfigurationStrategy.cs
└── Mapping/
    └── {Modulo}/
        ├── SqlServer/
        │   └── {Entidad}SqlServerConfiguration.cs (uno por entidad)
        ├── Postgres/
        │   └── {Entidad}PostgresConfiguration.cs
        └── Sqlite/
            └── {Entidad}SqliteConfiguration.cs
```
