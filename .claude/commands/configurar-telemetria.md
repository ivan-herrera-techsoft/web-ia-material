Lee el archivo `claude/skills/configurar-telemetria/SKILL.md` y sigue sus instrucciones para configurar OpenTelemetry en el proyecto actual.

Pasos obligatorios según el skill:
1. Agregar los paquetes NuGet de OpenTelemetry al `.csproj`
2. Crear `TelemetryConfiguration` en `Api/Dtos/Configurations/`
3. Agregar `Telemetry` a `GeneralConfiguration`
4. Crear `TelemetryConfigurationsReader.cs` en `Api/Extensions/Configuration/`
5. Crear `TraceEnricher.cs` en `Api/Helpers/Telemetry/`
6. Integrar `TraceEnricher` y sink de Loki condicionalmente en `LoggerConfigurationExtensions`
7. Agregar `ConfigureTelemetry()` en `ServiceExtensions.cs` con guard `if (!Enabled) return`
8. Agregar `UseOpenTelemetryPrometheusScrapingEndpoint()` condicionalmente en `Program.cs`
9. Agregar sección `Telemetry` en `appsettings.json` con `Enabled: false`
