Lee el archivo `claude/skills/crear-endpoint/SKILL.md` y sigue sus instrucciones para crear el endpoint de la Minimal API solicitado.

Endpoint a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Crear el DTO de request en `Application/Dtos/` (si aplica)
2. Crear el DTO de response en `Application/Dtos/` (si aplica)
3. Crear el validator de FluentValidation
4. Crear el endpoint en `Api/Endpoints/` como clase `partial static`
5. Registrar la ruta con `MapEndpoints` o `MapGroup`
6. Agregar versionado con `.HasApiVersion(ApiConstants.VERSION_1)`
