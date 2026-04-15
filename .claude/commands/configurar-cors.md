Lee el archivo `claude/skills/configurar-cors/SKILL.md` y sigue sus instrucciones para configurar CORS en el proyecto actual.

Pasos obligatorios según el skill:
1. Crear `CorsConfiguration` en `Api/Dtos/Configurations/`
2. Crear `CorsConfigurationsReader` en `Api/Extensions/Configuration/`
3. Implementar `ConfigureCors()` en `ServiceExtensions.cs` con la constante `ALLOW_ALL_CORS_POLICY`
4. Exponer el header `X-Pagination` con `.WithExposedHeaders("X-Pagination")`
5. Agregar sección `Cors` en `appsettings.json` con `AllowedOrigins`
6. Nunca usar `AllowAnyOrigin` en producción
7. Registrar `UseCors` como **primer** middleware en el pipeline
