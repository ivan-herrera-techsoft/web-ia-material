Lee el archivo `claude/skills/configurar-rate-limiting/SKILL.md` y sigue sus instrucciones para configurar el rate limiting en el proyecto actual.

Pasos obligatorios según el skill:
1. Crear `RateLimiterConfiguration` en `Api/Dtos/Configurations/`
2. Crear el `ConfigurationReader` correspondiente
3. Implementar `ConfigureRateLimiter()` en `ServiceExtensions.cs` con la constante `FIXED_RATE_LIMITING_POLICY`
4. Agregar `UseRateLimiter()` en el pipeline después de `UseCors`
5. Agregar sección `MaxCallsPerMinute` en `appsettings.json`
6. Aplicar la política a endpoints públicos o sensibles con `.RequireRateLimiting(FIXED_RATE_LIMITING_POLICY)`
