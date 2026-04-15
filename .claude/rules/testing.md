---
description: Convenciones para pruebas unitarias e integración — xUnit, Moq, patrón AAA y naming de métodos
globs: "**/Test/**,**/*Test.cs,**/*Tests.cs"
---

## Principios generales

- Framework: **xUnit** para pruebas, **Moq** para mocks
- Patrón: **AAA** (Arrange — Act — Assert) con comentarios de sección
- Todas las pruebas son independientes — sin estado compartido entre tests
- Cada test verifica **exactamente un comportamiento**

---

## Naming

| Elemento | Patrón | Ejemplo |
|---|---|---|
| Archivo | `{Clase}Test.cs` o `{Clase}Tests.cs` | `CanalDomainServiceTest.cs` |
| Clase | `{Clase}Test` | `public class CanalDomainServiceTest` |
| Método | `{Metodo}_{Escenario}_{ResultadoEsperado}` | `Guardar_NombreDuplicado_LanzaExcepcion()` |

---

## Estructura de un test

```csharp
namespace Company.Product.Module.Test.Domain.Services;

public class CanalDomainServiceTest
{
    private readonly Mock<ICanalRepository> _repositorioCanal;
    private readonly Mock<LoggerWrapper<CanalDomainService>> _logger;
    private readonly CanalDomainService _servicio;

    public CanalDomainServiceTest()
    {
        _repositorioCanal = new Mock<ICanalRepository>();
        _logger = new Mock<LoggerWrapper<CanalDomainService>>();
        _servicio = new CanalDomainService(_repositorioCanal.Object, _logger.Object);
    }

    [Fact]
    public async Task Guardar_DatosValidos_RetornaCanal()
    {
        // Arrange
        var nombre = "Canal de prueba";
        var descripcion = "Descripcion";
        _repositorioCanal
            .Setup(r => r.ConsultarCanales())
            .Returns(new List<Canal>().AsQueryable());

        // Act
        var resultado = await _servicio.Guardar(nombre, descripcion);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(nombre, resultado.Nombre);
        _repositorioCanal.Verify(r => r.Crear(It.IsAny<Canal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Guardar_NombreDuplicado_LanzaTInvalidOperationException()
    {
        // Arrange
        var nombre = "Canal existente";
        var canalExistente = new Canal(nombre, "desc");
        _repositorioCanal
            .Setup(r => r.ConsultarCanales())
            .Returns(new List<Canal> { canalExistente }.AsQueryable());

        // Act & Assert
        await Assert.ThrowsAsync<TInvalidOperationException>(
            () => _servicio.Guardar(nombre, "otra desc"));
    }
}
```

---

## Teorías — múltiples casos con [Theory]

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(null)]
public async Task Guardar_NombreInvalido_LanzaTArgumentException(string? nombre)
{
    // Act & Assert
    await Assert.ThrowsAsync<TArgumentException>(
        () => _servicio.Guardar(nombre!, "descripcion"));
}
```

---

## Mocking con Moq

```csharp
// Setup de retorno
_repositorio.Setup(r => r.ObtenerPorId(canalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canal);

// Setup de IQueryable
_repositorio.Setup(r => r.ConsultarCanales())
            .Returns(lista.AsQueryable());

// Verificar que se llamó
_repositorio.Verify(r => r.Crear(It.IsAny<Canal>(), It.IsAny<CancellationToken>()), Times.Once);
_repositorio.Verify(r => r.Crear(It.IsAny<Canal>(), It.IsAny<CancellationToken>()), Times.Never);

// Verificar con parámetro específico
_repositorio.Verify(r => r.Crear(It.Is<Canal>(c => c.Nombre == "test"), It.IsAny<CancellationToken>()), Times.Once);
```

---

## Qué se prueba por capa

| Capa | Qué probar | Mock |
|---|---|---|
| Domain Service | Reglas de negocio, excepciones, llamadas a repositorio | Repositorios, Logger |
| Application Service | Mapeo de DTOs, orquestación | Domain Services, Repositorios |
| Validator (FluentValidation) | Casos válidos e inválidos por campo | Ninguno |
| Entidad | Guards, métodos de dominio, invariantes | Ninguno |
| Endpoint | Happy path, error handling | Application Service |

---

## Estructura de directorios Test

```
Test/
├── Domain/
│   └── Services/
│       └── CanalDomainServiceTest.cs
├── Application/
│   └── Services/
│       └── CanalServiceTest.cs
├── Endpoints/
│   └── Canales/
│       └── CrearCanalEndpointTest.cs
└── Validators/
    └── CrearCanalValidatorTest.cs
```

---

## Reglas

- Sin sufijo Async en nombres de métodos de test aunque sean `async`
- Cada `[Fact]` prueba exactamente un caso
- `[Theory]` + `[InlineData]` para múltiples inputs del mismo escenario
- Mocks declarados como campos de clase — inicializados en el constructor
- Nunca compartir estado entre tests via campos estáticos o singletons
- `CancellationToken.None` o `It.IsAny<CancellationToken>()` para tests de async
