Lee el archivo `claude/skills/crear-application-service/SKILL.md` y sigue sus instrucciones para crear el servicio de aplicación solicitado.

Servicio a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Definir la interfaz del servicio
2. Implementar el servicio en `Application/Services/`
3. Inyectar el Domain Service o Repository correspondiente
4. Implementar los métodos CRUD usando la nomenclatura correcta: `ObtenerXxx`, `Guardar`, `Actualizar`, `Eliminar`
5. Todos los métodos deben ser `async` con `CancellationToken ct`
6. Registrar en el contenedor de DI
