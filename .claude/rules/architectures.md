---
description: Guía para elegir entre arquitecturas Hermes, Atenea y Titan según el alcance del proyecto
globs: "**/*.csproj,**/Program.cs"
---

## Las tres arquitecturas Bisoft

| Arquitectura | Alcance | Capas |
|---|---|---|
| **Hermes** | Proyecto pequeño, un solo propósito | Api + Aplicacion + Infraestructura, Dominio, Test |
| **Atenea** | Microservicio con BD propia | Api, Aplicacion, Dominio, Infraestructura, Shared, Test |
| **Titan** | Gran escala, multi-servicio | Gateway, Api, Aplicacion, Dominio, Infraestructura, Shared, Transversal, Test |

---

## Hermes — proyectos pequeños

**Cuándo usar:**
- Servicio con un solo propósito (proxy, adapter, conversor)
- Sin base de datos propia o solo con SQLite de configuración
- Equipo pequeño, sin necesidad de separación estricta de capas

**Estructura:**
```
Company.Product.Module/
├── Api/            (Endpoints + Application + Infrastructure colapsados)
├── Domain/
└── Test/
```

**Características:**
- Application e Infrastructure pueden vivir dentro de `Api/` sin proyectos separados
- Sin capa `Shared/` — las constantes y helpers van directamente en `Api/` o `Domain/`
- Ciclo de vida simple: un solo `DbContext` si hay BD

---

## Atenea — microservicios (arquitectura estándar)

**Cuándo usar:**
- Microservicio con base de datos propia
- Múltiples fuentes de datos posibles (BD + APIs externas)
- Equipo con más de un desarrollador
- Necesita escalar independientemente de otros servicios

**Estructura:**
```
Company.Product.Module/
├── Api/
├── Application/
├── Domain/
├── Infrastructure/
├── Shared/
└── Test/
```

**Características:**
- Separación estricta de capas con un proyecto por capa
- `Shared/` contiene constantes, DTOs y helpers transversales
- Un `DbContext` por módulo

---

## Titan — gran escala

**Cuándo usar:**
- Múltiples servicios coordinados dentro del mismo dominio
- Soporte multi-tenant
- Necesidad de Gateway como punto de entrada unificado
- Alto volumen con requisitos de observabilidad avanzados

**Estructura:**
```
Company.Product.Module/
├── Gateway/
├── Api/
├── Application/
├── Domain/
├── Infrastructure/
├── Shared/
├── Transversal/    (cross-cutting: logging, auth, correlación)
└── Test/
```

**Características:**
- `Transversal/` contiene: logging centralizado, correlación de requests, autenticación compartida
- Gateway maneja routing, autenticación y rate limiting para todos los servicios internos
- Múltiples `DbContext` (uno por subdomain)

---

## Preguntar al desarrollador

En proyectos nuevos, **siempre preguntar** qué arquitectura usar antes de generar código. La elección impacta la estructura de carpetas y la cantidad de proyectos `.csproj` en la solución. No asumir Atenea por defecto aunque sea la más común.
