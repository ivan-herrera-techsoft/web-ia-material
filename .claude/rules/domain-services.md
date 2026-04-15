---
description: Convenciones específicas para Domain Services — capa Domain/Services/
globs: "**/Domain/Services/**"
---

## Rol del Domain Service

El Domain Service encapsula las **reglas de negocio con acceso a datos**. Actua como intermediario entre la capa de Application y los repositorios. Es responsable de:

- Componer queries IQueryable con filtros de negocio
- Validar unicidad, estado y reglas cruzadas entre entidades
- Invocar los metodos de dominio de las entidades
- Confirmar cambios via `SaveChanges` con metadata

---

## Estructura

- Ubicacion: `Domain/Services/{Entidad}DomainService.cs`
- Nombre: `{Entidad}DomainService` — nunca `{Entidad}Service`
- Constructor principal; campos privados `_repositorio{Entidad}` y `_logger`
- Inyectar repositorios via interfaz (`I{Entidad}Repository`)
- Logger: `LoggerWrapper<{Entidad}DomainService>` — nunca `ILogger<T>`
- Todos los metodos publicos: `async Task` con `CancellationToken ct`
- Sin sufijo Async en nombres de metodos

---

## Naming de métodos

| Operacion | Patron | Ejemplo |
|---|---|---|
| IQueryable base | `Consultar{Entidad}s()` | `ConsultarCanales()` |
| Obtener por ID | `ObtenerPorId(id, ct)` | `ObtenerPorId(canalId, ct)` |
| Crear | `Guardar(params, ct)` | `Guardar(nombre, descripcion, ct)` |
| Actualizar | `Actualizar(id, params, ct)` | `Actualizar(canalId, nombre, ct)` |
| Eliminar | `Eliminar(id, ct)` | `Eliminar(canalId, ct)` |
| Toggle | `Alternar(id, ct)` | `Alternar(canalId, ct)` |
| Validaciones internas | `Validar{Regla}(params, ct)` | `ValidarNombreUnico(nombre, ct)` — `private` |

---

## Logging

- `LogDebug` al inicio de cada operacion de lectura o validacion interna (puede haber varios por metodo)
- `LogInformation` exactamente **uno** al final de cada metodo publico de escritura exitoso — describe el evento de dominio: `"Canal creado con id: {CanalId}"`
- **Nunca** interpolacion de strings: usar siempre parámetros nombrados `{CanalId}`

---

## Composicion de queries

El Domain Service compone el `IQueryable` con filtros de negocio antes de entregarlo al repositorio para materializacion:

```csharp
var query = _repositorioCanal.ConsultarCanales()
    .Where(c => c.Nombre == nombre)
    .OrderBy(c => c.Id);
var canal = await _repositorioCanal.ObtenerCanal(query, ct);
```

Cuando se requieren propiedades de navegacion, usar los metodos `Incluir*` del repositorio antes de filtrar:

```csharp
var query = _repositorioVersion.ConsultarVersiones();
query = _repositorioVersion.IncluirProyecto(query);
query = _repositorioVersion.IncluirConjuntoScripts(query);
var version = await _repositorioVersion.ObtenerVersion(
    query.Where(v => v.Id == versionId).OrderByDescending(v => v.FechaCreacionUtc), ct);
```

---

## Excepciones

- `TNotFoundException.EntityNotFound(msg, dict)` — cuando no se encuentra la entidad por ID
- `TInvalidOperationException(code, msg, dict)` — para reglas de negocio (nombre duplicado, estado inválido, etc.)
- Los códigos van en `DomainConstants.ExceptionCodes.Operation`

```csharp
if (existente is not null)
    throw new TInvalidOperationException(
        ExceptionCodes.Operation.CANAL_NOMBRE_DUPLICADO,
        "Ya existe un canal con el nombre {Nombre}",
        new Dictionary<string, object> { ["Nombre"] = nombre });
```

---

## SaveChanges con metadata

Confirmar cambios pasando el ID de la entidad en el diccionario de metadata. Esto permite al repositorio con caché invalidar su cache al recibir el ID:

```csharp
await _repositorioCanal.SaveChanges(
    new Dictionary<string, string> { ["CanalId"] = canal.Id.ToString() }, ct);
```

---

## Lo que el Domain Service NO hace

- **No** crea DTOs ni mapea con Mapster — eso es responsabilidad de Application Service
- **No** llama directamente a contextos de BD — solo a través de repositorios via interfaz
- **No** llama a servicios externos, HTTP, colas ni notificaciones — eso va en Application o Outbox
- **No** contiene lógica de presentación ni validaciones de formato de entrada (esas van en FluentValidation del endpoint o en `Formatear*()` de la entidad)
