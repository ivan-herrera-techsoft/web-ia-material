Lee el archivo `claude/skills/configurar-swagger/SKILL.md` y sigue sus instrucciones para configurar Swagger/OpenAPI en el proyecto actual.

Pasos obligatorios según el skill:
1. Agregar los paquetes NuGet de Asp.Versioning.Http y Swashbuckle al `.csproj`
2. Implementar `ConfigureSwagger()` en `ServiceExtensions.cs` con soporte multi-versión
3. Implementar `UseVersionedSwagger()` como extension method de `WebApplication`
4. Registrar `UseVersionedSwagger()` **solo en Development** en el pipeline
5. Aplicar `[SwaggerIgnore]` en endpoints que no deben aparecer en la documentación (ej. `/`)
