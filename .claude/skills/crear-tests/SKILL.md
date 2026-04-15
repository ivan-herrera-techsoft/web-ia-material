---
name: crear-tests
description: Genera tests unitarios para una clase de dominio, servicio o validator usando xUnit y Moq
argument-hint: <NombreClase a probar> ej. "CanalDomainService", "CrearCanalValidator", "Canal"
---

Crear las pruebas unitarias para **$ARGUMENTS**.

Antes de generar los tests, **preguntar al desarrollador**:
1. ¿Qué capa es la clase? (Domain Service, Application Service, Entidad, Validator, Endpoint)
2. ¿Qué métodos o comportamientos se deben cubrir?
3. ¿Existen casos de error específicos que probar? (nombre duplicado, ID no encontrado, valor nulo, etc.)

---

## Paso 1 — Identificar la clase y sus dependencias

Leer el archivo de la clase a probar e identificar:
- Dependencias inyectadas (repositorios, servicios, logger)
- Métodos públicos a probar
- Excepciones que puede lanzar (`TArgumentException`, `TInvalidOperationException`, `TNotFoundException`)

---

## Paso 2 — Crear el archivo de test

Crear `Test/{Capa}/{Modulo}/$ARGUMENTSTest.cs`:

```csharp
namespace {Namespace}.Test.{Capa}.{Modulo};

public class $ARGUMENTSTest
{
    // Mocks de dependencias
    private readonly Mock<I{Entidad}Repository> _repositorio{Entidad};
    private readonly Mock<LoggerWrapper<$ARGUMENTS>> _logger;

    // Subject under test
    private readonly $ARGUMENTS _servicio;

    public $ARGUMENTSTest()
    {
        _repositorio{Entidad} = new Mock<I{Entidad}Repository>();
        _logger = new Mock<LoggerWrapper<$ARGUMENTS>>();
        _servicio = new $ARGUMENTS(_repositorio{Entidad}.Object, _logger.Object);
    }

    // Tests...
}
```

---

## Paso 3 — Generar tests por método

Por cada método público, crear al menos:
1. **Happy path** — datos válidos, operación exitosa
2. **Error de argumento** — datos inválidos (`TArgumentException`)
3. **Error de negocio** — regla violada (`TInvalidOperationException`)
4. **Not found** — entidad no encontrada (`TNotFoundException`) cuando aplique

### Template happy path:

```csharp
[Fact]
public async Task {Metodo}_{Escenario}_RetornaResultadoEsperado()
{
    // Arrange
    // ... setup de mocks y datos

    // Act
    var resultado = await _servicio.{Metodo}(...);

    // Assert
    Assert.NotNull(resultado);
    // ... verificaciones específicas
    _repositorio{Entidad}.Verify(r => r.{OperacionEsperada}(...), Times.Once);
}
```

### Template error de negocio:

```csharp
[Fact]
public async Task {Metodo}_{Escenario}_LanzaTInvalidOperationException()
{
    // Arrange
    // ... setup que provoca el error

    // Act & Assert
    await Assert.ThrowsAsync<TInvalidOperationException>(
        () => _servicio.{Metodo}(...));
}
```

### Template con múltiples inputs ([Theory]):

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData(null)]
public async Task {Metodo}_{Campo}Invalido_LanzaTArgumentException(string? valor)
{
    // Act & Assert
    await Assert.ThrowsAsync<TArgumentException>(
        () => _servicio.{Metodo}(valor!, ...));
}
```

---

## Paso 4 — Verificar cobertura mínima

Confirmar que los tests cubren:

- [ ] Todos los métodos públicos tienen al menos un happy path
- [ ] Casos de error por argumento inválido (nulo, vacío, fuera de rango)
- [ ] Reglas de negocio que lanzan `TInvalidOperationException`
- [ ] `TNotFoundException` cuando busca por ID que no existe
- [ ] Verificaciones de que los mocks se llamaron el número esperado de veces
- [ ] El archivo compila sin errores (`dotnet build` en el proyecto Test)

---

## Reglas de naming

- Archivo: `{Clase}Test.cs`
- Método: `{Metodo}_{Escenario}_{ResultadoEsperado}`
- Sin sufijo Async aunque el método sea `async Task`
- Escenario y resultado en español, descriptivos
