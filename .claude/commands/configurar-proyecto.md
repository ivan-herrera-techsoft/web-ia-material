Lee el archivo `claude/skills/configurar-proyecto/SKILL.md` y sigue sus instrucciones para configurar la estructura inicial del proyecto Atenea.

Proyecto a configurar: $ARGUMENTS

Antes de comenzar, preguntar al desarrollador:
1. ¿Tipo de arquitectura? (Hermes, Atenea, Titan)
2. ¿Esquema de autenticación? (JWT, API Key, Cookies, OAuth 2.0/OIDC, Mixto)
3. ¿Proveedor de base de datos? (SqlServer, PostgreSQL, SQLite, multi-proveedor)
4. ¿Features opcionales? (Cache, Background Services, Telemetría, Notificaciones)

Pasos obligatorios según el skill:
1. Crear el proyecto desde el template NuGet `Bisoft.Templates.Atenea.MinimalApi`
2. Configurar `Program.cs` con el orden estricto de bootstrap (SetComponentPrefix → SetEncryption → GetConfiguration → 13 servicios → pipeline)
3. Configurar `appsettings.json` con las 9 secciones requeridas
4. Verificar estructura de 4 capas y `<Nullable>enable</Nullable>` en `.csproj`
5. Ejecutar `dotnet build` para confirmar compilación exitosa
