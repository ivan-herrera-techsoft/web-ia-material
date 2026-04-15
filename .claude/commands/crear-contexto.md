Lee el archivo `claude/skills/crear-contexto/SKILL.md` y sigue sus instrucciones para crear el DbContext y su infraestructura de base de datos.

Contexto a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Crear la Factory (`{Modulo}DbManagerStrategyFactory`) heredando de `ModelBuilderConfigurationsFactory`
2. Crear las Strategies por proveedor (`SqlServer`, `Postgres`, `Sqlite`) implementando `IModelBuilderConfigurations`
3. Crear el `DbContext` heredando de `ConfiguredDbContext<TFactory>`
4. Registrar en `ConfigureContexts()` via `DbContextOptionsBuilderStrategyFactory.Create(...)`
5. Aplicar convenciones de columnas: camelCase (SqlServer), snake_case (Postgres/Sqlite)
