Lee el archivo `claude/skills/crear-repositorio/SKILL.md` y sigue sus instrucciones para crear el repositorio solicitado.

Repositorio a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Definir la interfaz en `Domain/Contracts/Repositories/`
2. Implementar el repositorio en `Infrastructure/Repositories/` heredando de `EFRepository<TContext>`
3. Registrar en el método de extensión correspondiente (`ConfigureServices` o `InjectConfigurations`)
4. Usar `LoggerWrapper<T>` para logging
5. Decidir si retorna colección directa (catálogos) o `IQueryable` (datos variables)
