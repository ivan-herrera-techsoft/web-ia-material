Lee el archivo `claude/skills/configurar-api-versioning/SKILL.md` y sigue sus instrucciones para configurar el versionado de API en el proyecto actual.

Pasos obligatorios según el skill:
1. Agregar los paquetes NuGet de Asp.Versioning al `.csproj`
2. Crear `ApiConstants` con las constantes de versión (`VERSION_1`, etc.)
3. Implementar `ConfigureApiVersioning()` en `ServiceExtensions.cs`
4. Configurar versionado via header `X-Version`
5. Implementar `ConfigureSwagger()` con soporte multi-versión
6. Agregar `.HasApiVersion(ApiConstants.VERSION_1)` en cada endpoint
