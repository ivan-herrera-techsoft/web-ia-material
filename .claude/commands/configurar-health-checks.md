Lee el archivo `claude/skills/configurar-health-checks/SKILL.md` y sigue sus instrucciones para configurar los health checks en el proyecto actual.

Pasos obligatorios según el skill:
1. Implementar `ConfigureHealthChecks()` en `ServiceExtensions.cs` con checks de BD y dependencias externas
2. Registrar los 4 endpoints obligatorios via `AddHealthChecks()`:
   - `/health-check` — resumen simple (Healthy/Unhealthy)
   - `/health-details` — detalle de cada check con tiempos
   - `/health/live` — liveness probe (K8s)
   - `/health/ready` — readiness probe (K8s)
3. Agregar custom `HealthCheck` en `Api/HealthChecks/` si hay dependencias externas
4. Aplicar rate limiting a los endpoints de health
