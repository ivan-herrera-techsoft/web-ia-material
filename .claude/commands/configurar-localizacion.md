Lee el archivo `claude/skills/configurar-localizacion/SKILL.md` y sigue sus instrucciones para configurar la localización en el proyecto actual.

Culturas a soportar: $ARGUMENTS
(Ejemplo: es-MX, en-US — si no se especifica, usar es-MX como default)

Pasos obligatorios según el skill:
1. Crear los archivos de recursos `.resx` por cultura en `Api/Resources/`
2. Implementar `ConfigureLocalization()` en `ServiceExtensions.cs`
3. Registrar `UseRequestLocalization` en el pipeline después de `UseAuthorization`
4. Configurar la cultura por defecto y las culturas soportadas
5. Conectar con `Bisoft.Exceptions` para que los mensajes de error usen localización
