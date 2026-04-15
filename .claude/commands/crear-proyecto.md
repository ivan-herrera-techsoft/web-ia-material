Lee los archivos `claude/skills/configurar-proyecto/SKILL.md` y `claude/specs/crear-proyecto.md` y sigue sus instrucciones para crear y configurar un nuevo proyecto Atenea desde cero.

Nombre del proyecto: $ARGUMENTS

Pasos obligatorios:
1. Instalar el template NuGet `Bisoft.Templates.Atenea.MinimalApi` desde la fuente privada de Azure DevOps Artifacts
2. Crear el proyecto con `dotnet new bisoft-atenea`
3. Verificar y corregir `Program.cs` según el orden estricto de bootstrap:
   - `TException.SetComponentPrefix` — PRIMERA línea
   - `SetEncryption()` antes de `GetConfiguration()`
   - 13 servicios en orden exacto
   - Pipeline de middleware en orden exacto
4. Verificar `appsettings.json` con las 9 secciones requeridas
5. Verificar estructura de 4 capas (Api, Application, Domain, Infrastructure)
6. Ejecutar `dotnet build` para confirmar que compila
