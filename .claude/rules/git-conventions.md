---
description: Convenciones de Git — Conventional Commits y nomenclatura de ramas para proyectos Bisoft
globs: "**/.git/**"
---

## Ramas — kebab-case

| Tipo | Patrón | Ejemplo |
|---|---|---|
| Nueva funcionalidad | `feature/{descripcion}` | `feature/crear-canal-api` |
| Corrección de bug | `bugfix/{descripcion}` | `bugfix/correccion-login-jwt` |
| Release | `release/{version}` | `release/v1.2.0` |
| Hotfix urgente | `hotfix/{descripcion}` | `hotfix/correccion-token-expirado` |

**Reglas de nombre de rama:**
- Siempre kebab-case — sin espacios, sin mayúsculas, sin caracteres especiales
- Descripción concisa (máximo 4-5 palabras) en español
- Nunca trabajar directamente en `main` o `master`

---

## Commits — Conventional Commits

**Formato:** `[tipo]: descripcion en presente e imperativo`

| Tipo | Cuándo usar | Ejemplo |
|---|---|---|
| `feat` | Nueva funcionalidad | `feat: agregar endpoint de creacion de canales` |
| `fix` | Corrección de bug | `fix: corregir validacion de token expirado` |
| `docs` | Documentación | `docs: actualizar README con nuevos endpoints` |
| `style` | Formato, sin cambio de lógica | `style: aplicar formato a CanalService` |
| `refactor` | Refactoring sin nueva funcionalidad | `refactor: extraer logica de paginacion a extension` |
| `test` | Agregar o modificar tests | `test: agregar pruebas para CanalDomainService` |
| `chore` | Tareas de mantenimiento | `chore: actualizar dependencias NuGet` |

**Reglas de mensaje de commit:**
- Descripción en minúsculas y en español
- Sin punto final
- Máximo 72 caracteres en la primera línea
- Usar presente imperativo: "agregar", "corregir", "actualizar" — no "agregado", "corregido"

---

## Variables de entorno — UPPER_SNAKE_CASE

Las variables de entorno que sobreescriben configuración de appsettings usan `UPPER_SNAKE_CASE`:

```bash
DATABASE_URL=Server=...
API_KEY=abc123
TELEMETRY_ENABLED=true
JWT_KEY=secreto
```

Nunca usar camelCase ni PascalCase para variables de entorno.
