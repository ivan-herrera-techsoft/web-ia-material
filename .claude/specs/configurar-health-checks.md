# Spec: Health Checks

## Propósito

Define los contratos que debe cumplir la configuración de health checks en la capa API. Garantiza que todo proyecto exponga los cuatro endpoints obligatorios con la semántica correcta de liveness y readiness, y que los checks personalizados usen las abstracciones internas del proyecto.

---

## Contratos de endpoints

### SC-HC-01: Cuatro endpoints obligatorios

Todo proyecto expone exactamente estos cuatro endpoints de salud, sin excepción:

| Endpoint          | Semántica                                           |
|-------------------|-----------------------------------------------------|
| `/health-check`   | Estado general de la aplicación, sin filtro         |
| `/health-details` | Detalle JSON completo vía `UIResponseWriter`        |
| `/health/live`    | Liveness — solo verifica que el proceso está vivo   |
| `/health/ready`   | Readiness — verifica que puede recibir tráfico      |

Queda prohibido exponer solo un subconjunto de estos endpoints o combinar liveness y readiness en un mismo endpoint.

### SC-HC-02: Anonimato y separación de acceso

`/health-check` y `/health-details` son anónimos (`AllowAnonymous`). `/health/live` y `/health/ready` no declaran autorización explícita — heredan la política del pipeline.

### SC-HC-03: Clase partial WebApplicationExtensions

El mapeo de los cuatro endpoints vive en `Api/Extensions/Endpoints/HealthChecksMapping.cs` como `public static partial class WebApplicationExtensions`. El uso de `partial` permite coexistir con los otros extension methods del mismo nombre definidos en `WebApplicationExtensions.cs`.

```csharp
// CORRECTO
public static partial class WebApplicationExtensions { ... }

// INCORRECTO — clase separada sin partial
public static class HealthChecksMappingExtensions { ... }
```

### SC-HC-04: Predicate de liveness y readiness

- `/health/live` filtra por nombre fijo `"Liveness"`: `Predicate = (check) => check.Name == "Liveness"`
- `/health/ready` filtra por tag: `Predicate = (check) => check.Tags.Contains("ready")`
- `/health-check` y `/health-details` no tienen predicate — evalúan todos los checks

---

## Contratos de checks

### SC-HC-05: Check de liveness como lambda

El check de liveness es siempre una lambda inline registrada con el nombre fijo `"Liveness"`. Retorna `Healthy` con la versión del ensamblado. No es una clase `IHealthCheck`.

```csharp
// CORRECTO
.AddCheck("Liveness", () => HealthCheckResult.Healthy($"API iniciada correctamente. v{ASSEMBLY_VERSION}"))

// INCORRECTO — clase separada para liveness
.AddCheck<LivenessHealthCheck>("Liveness")
```

### SC-HC-06: DatabaseHealthCheck vía ConnectionStringValidatorFactory

Los checks de BD usan `DatabaseHealthCheck`, que verifica conectividad a través de `ConnectionStringValidatorFactory` de `Bisoft.DatabaseConnections`. Queda prohibido usar `DbContext.Database.CanConnectAsync()` o abrir conexiones directamente.

```csharp
// CORRECTO
.AddCheck("Storage", new DatabaseHealthCheck(configuration.{NombreConexion}), tags: ["ready"])

// INCORRECTO — usar EF Core directamente
.AddDbContextCheck<AppContext>("base-datos")
```

Un check de BD por cada contexto del proyecto. Todos con tag `"ready"`.

### SC-HC-07: HttpHealthCheck para servicios externos

Los checks de servicios HTTP externos usan `HttpHealthCheck`. Soporta un paso de autenticación previo: si se pasan `urlLogin` y `loginRequest`, primero hace GET al `_url` y luego POST al `_urlLogin`. Cualquier status non-2xx resulta en `Unhealthy`.

```csharp
// Sin autenticacion
.AddCheck("ServicioExterno", new HttpHealthCheck(configuration.UrlServicio), tags: ["ready"])

// Con autenticacion previa
.AddCheck("ServicioExterno", new HttpHealthCheck(
    configuration.UrlServicio,
    configuration.UrlLogin,
    new { usuario = configuration.Usuario, password = configuration.Password }
), tags: ["ready"])
```

### SC-HC-08: Tag "ready" en todos los checks que no son liveness

Cualquier check que no sea el de liveness debe tener el tag `"ready"` para que `/health/ready` lo evalúe. Un check sin tag `"ready"` nunca aparecerá en readiness.

```csharp
.AddCheck("Storage", new DatabaseHealthCheck(...), tags: ["ready"])   // ✓
.AddCheck("Liveness", () => HealthCheckResult.Healthy(...))           // ✓ sin tag — solo liveness
.AddCheck("Storage", new DatabaseHealthCheck(...))                     // ✗ falta tag "ready"
```

---

## Contratos de registro

### SC-HC-09: ConfigureHealthChecks en ServiceExtensions

El registro de checks vive en `ServiceExtensions.ConfigureHealthChecks`. No se registran checks en `Program.cs` ni en otros métodos.

### SC-HC-10: AddHealthChecks en Program.cs con rate limiting

En proyectos con rate limiting, `app.AddHealthChecks` recibe la política como parámetro. En proyectos sin rate limiting, se usa el overload sin parámetros. Nunca mezclar los dos overloads.

```csharp
// Con rate limiting
app.AddHealthChecks(FIXED_RATE_LIMITING_POLICY);

// Sin rate limiting
app.AddHealthChecks();
```
