Lee el archivo `claude/skills/crear-background-service/SKILL.md` y sigue sus instrucciones para crear el background service solicitado.

Background service a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Crear el DTO de configuración en `Api/Dtos/Configurations/`
2. Agregar la propiedad al `GeneralConfiguration`
3. Crear el `ConfigurationReader` en `Api/Extensions/Configuration/`
4. Agregar la llamada en `GetConfiguration()`
5. Crear el servicio en `Api/BackgroundServices/` heredando de `TimedBackgroundService`
6. Registrar condicionalmente en `ConfigureAutomatedServices`
7. Agregar la sección en `appsettings.json`
