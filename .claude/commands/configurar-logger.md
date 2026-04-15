Lee el archivo `claude/skills/configurar-logger/SKILL.md` y sigue sus instrucciones para configurar Serilog en el proyecto actual.

Pasos obligatorios según el skill:
1. Agregar paquetes NuGet de Serilog al `.csproj`
2. Crear `LoggerConfiguration` en `Api/Dtos/Configurations/`
3. Crear `LoggerConfigurationsReader` en `Api/Extensions/Configuration/`
4. Implementar `ConfigureLogger()` con los sinks:
   - Console: siempre activo
   - SQLite: solo en Development
   - GrafanaLoki: solo en producción (condicional con Telemetry)
5. Agregar sección `Logger` en `appsettings.json`
6. Nunca usar interpolación de strings en logging — siempre parámetros nombrados
