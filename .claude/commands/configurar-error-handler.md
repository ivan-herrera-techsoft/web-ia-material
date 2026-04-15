Lee el archivo `claude/skills/configurar-error-handler/SKILL.md` y sigue sus instrucciones para configurar el middleware de manejo de errores en el proyecto actual.

Pasos obligatorios según el skill:
1. Crear `ErrorHandlerMiddleware` en `Api/Middleware/`
2. Capturar excepciones de `Bisoft.Exceptions` (`TArgumentException`, `TInvalidOperationException`, `TEnvironmentException`) y mapearlas a códigos HTTP apropiados
3. Retornar `ProblemDetails` estandarizado (nunca exponer stack traces en producción)
4. Registrar `UseMiddleware<ErrorHandlerMiddleware>` como **último** middleware antes de los endpoints en el pipeline
5. Loguear errores con `LogError` para excepciones no controladas, `LogWarning` para fallos esperados
