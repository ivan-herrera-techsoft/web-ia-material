Lee el archivo `claude/skills/configurar-home/SKILL.md` y sigue sus instrucciones para configurar la página de inicio (Home) en el proyecto actual.

Nombre de la aplicación: $ARGUMENTS

Pasos obligatorios según el skill:
1. Reemplazar el placeholder `APP_NAME` con el nombre real de la aplicación
2. Crear `HomeMapping.cs` en `Api/Extensions/Endpoints/` como clase `partial static WebApplicationExtensions`
3. Implementar los dos overloads de `MapHome()` y el helper privado `Home()`
4. Usar `[AllowAnonymous]` y `SwaggerIgnoreAttribute` en el endpoint raíz `/`
5. Verificar el health check via `HealthCheckService` en la página
6. Encadenar `MapHome(FIXED_RATE_LIMITING_POLICY)` en `Program.cs` después de `AddHealthChecks`
