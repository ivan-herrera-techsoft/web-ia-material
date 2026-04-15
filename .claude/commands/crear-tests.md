Lee el archivo `claude/skills/crear-tests/SKILL.md` y sigue sus instrucciones para crear las pruebas unitarias de la clase solicitada.

Clase a probar: $ARGUMENTS

Antes de comenzar, preguntar al desarrollador:
1. ¿Qué capa es la clase? (Domain Service, Application Service, Entidad, Validator, Endpoint)
2. ¿Qué métodos se deben cubrir?
3. ¿Hay casos de error específicos? (nombre duplicado, ID no encontrado, valor nulo, etc.)

Pasos obligatorios según el skill:
1. Leer el archivo fuente de la clase para identificar dependencias y métodos públicos
2. Crear `Test/{Capa}/{Modulo}/{Clase}Test.cs` con mocks en el constructor
3. Generar al menos un happy path y un test de error por cada método público
4. Usar `[Theory]` + `[InlineData]` para múltiples inputs del mismo escenario
5. Verificar interacciones con `Verify(..., Times.Once)` tras operaciones de escritura
6. Ejecutar `dotnet build` en el proyecto Test para confirmar compilación exitosa
