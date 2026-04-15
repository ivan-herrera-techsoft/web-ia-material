Lee el archivo `claude/skills/configurar-autenticacion/SKILL.md` y sigue sus instrucciones para configurar la autenticación en el proyecto actual.

Esquema de autenticación: $ARGUMENTS
(Opciones: JWT, ApiKey, Cookies, OAuth/OIDC, Mixto — si no se especifica, preguntar al desarrollador)

Pasos obligatorios según el skill:
1. Agregar los paquetes NuGet correspondientes al esquema elegido
2. Crear `AuthConfiguration` / `JwtConfiguration` en `Api/Dtos/Configurations/`
3. Crear el `ConfigurationReader` correspondiente
4. Implementar `ConfigureAuthentication()` en `ServiceExtensions.cs`
5. Si usa JWT: implementar `/auth/refresh` endpoint con `Bisoft.Security.RefreshTokens.EntityFramework`
6. Agregar sección de configuración en `appsettings.json`
7. Nunca hardcodear secretos — usar `SensitiveData` en appsettings
