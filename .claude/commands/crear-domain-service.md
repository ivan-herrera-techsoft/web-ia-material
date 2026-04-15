Lee el archivo `claude/skills/crear-domain-service/SKILL.md` y sigue sus instrucciones para crear el domain service solicitado.

Domain service a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Definir la interfaz en `Domain/Contracts/` (si aplica separacion de interfaz)
2. Implementar el servicio en `Domain/Services/` con sufijo `DomainService`
3. Inyectar el repositorio via interfaz (`IEFRepository`)
4. Implementar métodos con nomenclatura de dominio: `Consultar{Entidad}s()` (IQueryable), `ObtenerPorId`, `Guardar`, etc.
5. Registrar en el contenedor de DI como Scoped
6. Nunca acceder a infraestructura directamente — solo via contratos
