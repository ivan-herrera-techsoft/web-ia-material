# Spec: Pruebas Unitarias

## Propósito

Define los contratos que toda prueba unitaria DEBE cumplir en el proyecto. Aplica a la capa `Test/` y garantiza consistencia en estructura, naming y cobertura mínima.

---

## Contratos obligatorios

### SC-TEST-01: Framework xUnit + Moq

Las pruebas DEBEN usar **xUnit** como framework de testing y **Moq** para mocks. No usar NUnit, MSTest ni otras librerías de mocking.

---

### SC-TEST-02: Naming de archivos y clases

El archivo de test lleva el sufijo `Test` (no `Tests`) y el nombre de la clase que prueba:

```
CanalDomainService → CanalDomainServiceTest.cs
CrearCanalValidator → CrearCanalValidatorTest.cs
Canal → CanalTest.cs
```

La clase de test es `public` y no hereda de ninguna clase base.

---

### SC-TEST-03: Naming de métodos — patrón obligatorio

```
{Metodo}_{Escenario}_{ResultadoEsperado}
```

| Componente | Descripción | Ejemplo |
|---|---|---|
| `{Metodo}` | Nombre del método que se prueba | `Guardar` |
| `{Escenario}` | Condición de entrada | `NombreDuplicado` |
| `{ResultadoEsperado}` | Qué debe ocurrir | `LanzaExcepcion` |

**Correcto:** `Guardar_NombreDuplicado_LanzaTInvalidOperationException`
**Incorrecto:** `TestGuardar`, `Guardar_Test`, `PruebaDeGuardar`

Sin sufijo Async aunque el método sea `async Task`.

---

### SC-TEST-04: Patrón AAA obligatorio con comentarios

Cada test DEBE tener las tres secciones comentadas:

```csharp
// Arrange
// Act
// Assert
```

Si `Act` y `Assert` se combinan (ej. `Assert.ThrowsAsync`), se usa `// Act & Assert`.

---

### SC-TEST-05: Dependencias como campos de clase

Las dependencias (mocks y subject under test) se declaran como campos privados de la clase y se inicializan en el constructor:

```csharp
public class CanalDomainServiceTest
{
    private readonly Mock<ICanalRepository> _repositorioCanal;
    private readonly CanalDomainService _servicio;

    public CanalDomainServiceTest()
    {
        _repositorioCanal = new Mock<ICanalRepository>();
        _servicio = new CanalDomainService(_repositorioCanal.Object, ...);
    }
}
```

**Prohibido:** inicializar mocks dentro de cada método de test.

---

### SC-TEST-06: Cobertura mínima por método público

Todo método público DEBE tener al menos:
- Un test de happy path (datos válidos, operación exitosa)
- Un test por cada excepción tipada que puede lanzar (`TArgumentException`, `TInvalidOperationException`, `TNotFoundException`)

---

### SC-TEST-07: [Theory] para inputs múltiples del mismo escenario

Cuando el mismo comportamiento se verifica con distintos valores de entrada, usar `[Theory]` + `[InlineData]`. No crear un `[Fact]` por cada valor.

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(null)]
public async Task Guardar_NombreInvalido_LanzaTArgumentException(string? nombre) { ... }
```

---

### SC-TEST-08: Estructura de directorios espeja las capas

```
Test/
├── Domain/
│   ├── Services/       → tests de Domain Services
│   └── Entities/       → tests de entidades y validators
├── Application/
│   └── Services/       → tests de Application Services
├── Endpoints/          → tests de endpoints (integration-style)
└── Validators/         → tests de FluentValidation validators
```

---

### SC-TEST-09: Verificar interacciones con Verify

Después de una operación de escritura, SIEMPRE verificar que el repositorio fue llamado con `Times.Once` o `Times.Never`:

```csharp
_repositorioCanal.Verify(r => r.Crear(It.IsAny<Canal>(), It.IsAny<CancellationToken>()), Times.Once);
```

Para operaciones que no deben ocurrir:

```csharp
_repositorioCanal.Verify(r => r.Crear(It.IsAny<Canal>(), It.IsAny<CancellationToken>()), Times.Never);
```

---

### SC-TEST-10: El proyecto Test compila sin errores

Tras generar o modificar tests, ejecutar `dotnet build` en el proyecto Test y confirmar que no hay errores de compilación ni warnings de referencias nulas.
