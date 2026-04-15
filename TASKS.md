# TASKS.md — Plan de Desarrollo: Frontend + Login
**Proyecto:** SIMA Web — Registro de Actividades  
**Fecha:** 2026-04-14  
**Metodología:** Spec-Driven Development

---

## Datos de Referencia Globales

| Dato | Valor |
|------|-------|
| Frontend URL (dev) | `http://localhost:4200` |
| Backend URL (dev) | `http://localhost:5000` |
| Tenant ID (Azure) | `e3821ff1-5752-48ab-b6d8-718c531dc602` |
| Authority URL | `https://login.microsoftonline.com/e3821ff1-5752-48ab-b6d8-718c531dc602/v2.0` |
| **SPA** — Display name | `SIMA-SPA` |
| **SPA** — Client ID | `16292989-8579-4359-92de-7c8ac308fe21` |
| **API** — Display name | `sima-api` |
| **API** — Client ID | `05d08310-7ecf-4c11-9761-1cf8adb13e34` |
| **API** — Scope | `api://05d08310-7ecf-4c11-9761-1cf8adb13e34/access_as_user` |
| Roles Entra ID | `superusuario`, `administrador`, `lider`, `usuario` |
| Nombre solución .NET | `Techsoft.Sima.Web` |
| Nombre proyecto Angular | `sima-web` |
| Prefijo de selectores | `sima-` |
| Base de datos | `Sima-webtech` en `lap-ivan\MSSQLSERVER01` |

---

## Fase 1 — Base de Datos

| # | Área | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|-------|-------------|-------------|------------|--------|
| 1.1 | BD | Crear base de datos | Crear la BD `Sima-webtech` en el servidor SQL Server | Servidor: `lap-ivan\MSSQLSERVER01` | — | ✅ Completado |
| 1.2 | BD | Tabla `Usuarios` | Schema `usr`; Id (uniqueidentifier), Email (unique), Nombre, ApellidoPaterno, ApellidoMaterno, Estatus (tinyint 0-3), CreadoPorId (FK self), FechaCreacionUtc | — | 1.1 | ✅ Completado |
| 1.3 | BD | Tabla `SolicitudesAcceso` | Schema `usr`; Id, Email, FechaSolicitudUtc, Estatus (tinyint 0-2), AtendidaPorId (FK Usuarios), FechaAtencionUtc | — | 1.1 | ✅ Completado |

---

## Fase 2 — Backend: Proyecto Base y Configuración

| # | Área | Capa | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|------|-------|-------------|-------------|------------|--------|
| 2.1 | Backend | Solución | Crear solución .NET | Scaffold solución `Techsoft.Sima.Web` con arquitectura Atenea (Api, Application, Domain, Infrastructure, Shared, Test) | Namespace: `Techsoft.Sima.Web` | — | ✅ Completado |
| 2.2 | Backend | Api | Configurar Program.cs | Bootstrap con orden obligatorio: GetConfiguration → servicios → pipeline | Prefix: `SWB` | 2.1 | ✅ Completado |
| 2.3 | Backend | Api | Configurar Logger | Serilog con sink SQLite (dev) y SQL Server (prod) vía `LoggerConfigurationExtensions` | BD: `Sima-webtech` | 2.1 | ✅ Completado |
| 2.4 | Backend | Api | Configurar CORS | Política `AllowAll` con origen `http://localhost:4200`; header expuesto `X-Pagination` | Origins: `http://localhost:4200` | 2.1 | ✅ Completado |
| 2.5 | Backend | Api | Configurar Rate Limiting | `FixedWindowLimiter` con política `Fixed`; 100 llamadas/minuto (configurable) | `MaxCallsPerMinute: 100` | 2.1 | ✅ Completado |
| 2.6 | Backend | Api | Configurar API Versioning | Versionado por header `X-Version`; versión por defecto `1.0` | — | 2.1 | ✅ Completado |
| 2.7 | Backend | Api | Configurar Swagger | Solo en desarrollo; `SwaggerConfig`, `SwaggerIgnoreAttribute` | — | 2.6 | ✅ Completado |
| 2.8 | Backend | Api | Configurar Health Checks | 4 endpoints: `/health-check`, `/health-details`, `/health/live`, `/health/ready` + `DatabaseHealthCheck` | BD: `Sima-webtech` | 2.1 | ✅ Completado |
| 2.9 | Backend | Api | Configurar Home | Página de bienvenida en `/` con health checks embebidos | — | 2.8 | ✅ Completado |
| 2.10 | Backend | Api | Configurar Localización | `SharedResources` con `es-MX` (default) y `es-GT`; claves para excepciones | — | 2.1 | ✅ Completado |
| 2.11 | Backend | Api | Configurar Error Handler | `ErrorHandlerMiddleware` con mapeo de excepciones → HTTP y localización | — | 2.10 | ✅ Completado |

---

## Fase 3 — Backend: Autenticación con Azure Entra ID

| # | Área | Capa | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|------|-------|-------------|-------------|------------|--------|
| 3.1 | Backend | Api | Configurar JWT Bearer | Validar tokens emitidos por Azure Entra ID para `sima-api`; validar `aud`, `iss`, `roles` claim | Authority: `https://login.microsoftonline.com/e3821ff1.../v2.0` / Audience: `05d08310-...` | 2.2 | ✅ Completado |
| 3.2 | Backend | Domain | Entidad `Usuario` | Clase de dominio con validadores (`FormatearEmail`, `FormatearNombre`, etc.) y máquina de estados | — | 2.1 | ✅ Completado |
| 3.3 | Backend | Domain | Interfaz `IUsuarioRepository` | `ConsultarUsuarios()`, `ObtenerPorEmail()`, `ObtenerPorId()`, `Crear()`, `Actualizar()` | — | 3.2 | ✅ Completado |
| 3.4 | Backend | Infrastructure | Mapping EF Core `Usuario` | `UsuarioSqlServerConfiguration` con tipos de columna correctos; tabla `Usuarios` | — | 3.3 | ✅ Completado |
| 3.5 | Backend | Infrastructure | `UsuarioRepository` | Implementación de `IUsuarioRepository` con `EFRepositoryBase<SimaContext, UsuarioRepository>` | — | 3.4 | ✅ Completado |
| 3.6 | Backend | Infrastructure | `SimaContext` | DbContext con `DbSet<Usuario>` + `DbSet<SolicitudAcceso>`; `OnModelCreating` aplica configuraciones SqlServer | BD: `Sima-webtech` | 3.5 | ✅ Completado |
| 3.7 | Backend | Domain | Entidad `SolicitudAcceso` | Clase de dominio con Email, FechaSolicitud, Estatus | — | 2.1 | ✅ Completado |
| 3.8 | Backend | Domain | Interfaz `ISolicitudAccesoRepository` | `ConsultarSolicitudes()`, `ObtenerPorEmailYEstatus()`, `Crear()`, `Actualizar()` | — | 3.7 | ✅ Completado |
| 3.9 | Backend | Infrastructure | Mapping EF Core `SolicitudAcceso` | `SolicitudAccesoSqlServerConfiguration` | — | 3.8 | ✅ Completado |
| 3.10 | Backend | Infrastructure | `SolicitudAccesoRepository` | Implementación de `ISolicitudAccesoRepository` | — | 3.9 | ✅ Completado |
| 3.11 | Backend | Domain | `UsuarioDomainService` | Validar email único, crear y activar usuario, `SaveChanges` con metadata | — | 3.5 | ✅ Completado |
| 3.12 | Backend | Domain | `SolicitudAccesoDomainService` | Crear solicitud, validar sin pendiente existente, aceptar/rechazar, `AceptarYObtener()` | — | 3.10 | ✅ Completado |
| 3.13 | Backend | Application | `UsuarioService` | `ObtenerPerfil(email)`, `ObtenerUsuarios()` con proyección IQueryable | — | 3.11 | ✅ Completado |
| 3.14 | Backend | Application | `SolicitudAccesoService` | `Solicitar()`, `ObtenerSolicitudesPendientes()`, `Aceptar(id, rol)`, `Rechazar(id)` | — | 3.12 | ✅ Completado |
| 3.15 | Backend | Api | Endpoint `GET /api/auth/me` | Retorna perfil del usuario autenticado (rol, nombre, email) desde claims + BD | Requiere auth JWT | 3.13 | ✅ Completado |
| 3.16 | Backend | Api | Endpoint `POST /api/acceso/solicitar` | Guarda solicitud de acceso para usuario no registrado; `[AllowAnonymous]` | — | 3.14 | ✅ Completado |
| 3.17 | Backend | Api | Endpoint `GET /api/acceso/solicitudes` | Lista solicitudes pendientes paginadas; solo Administrador/Superusuario | — | 3.14 | ✅ Completado |
| 3.18 | Backend | Api | Endpoint `POST /api/acceso/aceptar/{id}` | Acepta solicitud y crea usuario con rol indicado; solo Administrador/Superusuario | — | 3.14 | ✅ Completado |
| 3.19 | Backend | Api | Endpoint `POST /api/acceso/rechazar/{id}` | Rechaza solicitud de acceso; solo Administrador/Superusuario | — | 3.14 | ✅ Completado |

---

## Fase 4 — Frontend: Proyecto Base y Configuración

| # | Área | Capa | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|------|-------|-------------|-------------|------------|--------|
| 4.1 | Frontend | Config | Crear proyecto Angular | `ng new sima-web` con routing y sin SSR; standalone components | `sima-web` | — | ✅ Completado |
| 4.2 | Frontend | Config | Instalar dependencias | `angular-auth-oidc-client`, `@jsverse/transloco`, Tailwind CSS v3.4, Vitest | Ver lista completa abajo | 4.1 | ✅ Completado |
| 4.3 | Frontend | Config | Configurar Tailwind CSS | `tailwind.config.js` con tokens semánticos de DESIGN-TOKENS; `tailwind.config.js` con `darkMode: ["selector", '[data-theme="dark"]']` | DESIGN-TOKENS.md | 4.1 | ✅ Completado |
| 4.4 | Frontend | Config | Configurar estilos base | `src/styles/themes/`: `_tokens.css`, `_theme-light.css`, `_theme-dark.css`; `components.css`; `index.css` | — | 4.3 | ✅ Completado |
| 4.5 | Frontend | Config | Crear `environments` | `environment.ts` y `environment.prod.ts` con `apiUrl`, `clientId`, `authority`, `scope` | ClientID SPA: `16292989-...` / Scope: `api://05d08310-...` | 4.1 | ✅ Completado |
| 4.6 | Frontend | Config | Crear objeto THEME | `src/app/theme/theme.ts` con variantes button, badge, card, input + tipos derivados | UI-COMPONENTS.md | 4.1 | ✅ Completado |
| 4.7 | Frontend | Config | Configurar Transloco | `app.config.ts`; archivos `src/assets/i18n/es.json` y `en.json` con claves base | — | 4.1 | ✅ Completado |
| 4.8 | Frontend | Config | Configurar Vitest | `vitest.config.ts` con jsdom; script `npm test` | — | 4.1 | ✅ Completado |

---

## Fase 5 — Frontend: Autenticación con Azure Entra ID

| # | Área | Capa | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|------|-------|-------------|-------------|------------|--------|
| 5.1 | Frontend | Auth | `auth.config.ts` | Configurar `angular-auth-oidc-client` con Authority, ClientID de SIMA-SPA, scope de sima-api, `responseType: 'code'`, `secureRoutes` solo al API | Authority: `https://login.microsoftonline.com/e3821ff1.../v2.0` / ClientID: `16292989-...` / Scope: `api://05d08310-.../access_as_user` | 4.5 | ✅ Completado |
| 5.2 | Frontend | Auth | `auth.service.ts` | Wrapea `OidcSecurityService`; expone signals: `isAuthenticated`, `userData`, `email`, `roles`; método `hasRole(role)`, `canManageX()` por permiso | Roles: `superusuario`, `administrador`, `lider`, `usuario` | 5.1 | ✅ Completado |
| 5.3 | Frontend | Auth | `permission.guard.ts` | Guard funcional síncrono de fábrica: `permissionGuard(fn => fn.canManageX())` | — | 5.2 | ✅ Completado |
| 5.4 | Frontend | Auth | `user-registered.guard.ts` | Guard asíncrono: llama `GET /api/auth/me`; si 404 redirige a `/solicitar-acceso`; si OK carga permisos en `AuthService` | `GET /api/auth/me` | 5.2, 3.15 | ✅ Completado |
| 5.5 | Frontend | Auth | Página `callback` | Manejo del retorno de Azure Entra ID después del login; componente de carga mientras se procesa el token | — | 5.1 | ✅ Completado |
| 5.6 | Frontend | Auth | Página `acceso-denegado` | Página visible para usuarios autenticados en Azure pero sin registro en la app; muestra opción de solicitar acceso | — | 5.1 | ✅ Completado |
| 5.7 | Frontend | Auth | Componente solicitud de acceso | Formulario mínimo (email pre-llenado desde claims, botón solicitar); llama `POST /api/acceso/solicitar`; mensaje de confirmación | `POST /api/acceso/solicitar` | 5.6, 3.16 | ✅ Completado |
| 5.8 | Frontend | Config | HTTP Interceptor | Adjunta el access token de `OidcSecurityService` a todas las peticiones hacia `environment.apiUrl`; `secureRoutes` en `auth.config.ts` | — | 5.1 | ✅ Completado |

---

## Fase 6 — Frontend: Layout Base y Rutas

| # | Área | Capa | Tarea | Descripción | Datos clave | Depende de | Estado |
|---|------|------|-------|-------------|-------------|------------|--------|
| 6.1 | Frontend | Layout | `app.ts` | Componente raíz con `RouterOutlet`; inicializa OIDC en constructor; aplica tema de `ThemeService` | — | 5.1 | ✅ Completado |
| 6.2 | Frontend | Layout | `app.config.ts` | `provideRouter`, `provideHttpClient(withInterceptorsFromDi())`, `provideAuth(authConfig)`, `provideTransloco(...)` | — | 5.1, 4.7 | ✅ Completado |
| 6.3 | Frontend | Layout | `app.routes.ts` | Rutas públicas (`/callback`, `/acceso-denegado`) + rutas protegidas con `autoLoginPartialRoutesGuard` + `userRegisteredGuard`; shell con sidebar y header | — | 5.3, 5.4 | ✅ Completado |
| 6.4 | Frontend | Layout | Componente `Sidebar` | Navegación lateral con menú dinámico según rol; colapsable; links con `RouterLink`; items ocultos si no tiene permiso | — | 5.2 | ✅ Completado |
| 6.5 | Frontend | Layout | Componente `Header` | Barra superior con nombre del usuario (desde `AuthService`), botón logout (`OidcSecurityService.logoff()`), toggle de tema | — | 5.2 | ✅ Completado |
| 6.6 | Frontend | Layout | `ThemeService` + toggle | Persiste tema en `StorageAdapter` (sessionStorage); aplica `data-theme` en `<html>`; script anti-FOUC en `index.html` | — | 4.4 | ✅ Completado |
| 6.7 | Frontend | Shared | Componentes UI base | `AppButtonComponent`, `AppBadgeComponent`, `AppCardComponent`, `AppSelectComponent` siguiendo THEME | UI-COMPONENTS.md | 4.6 | ✅ Completado |
| 6.8 | Frontend | Shared | Storage Adapter | `InjectionToken<StorageAdapter>` + implementación browser (sessionStorage) en `shared/platform/` | — | 4.1 | ✅ Completado |
| 6.9 | Frontend | Layout | Página `Dashboard` | Página de inicio post-login: saludo con nombre del usuario; placeholder para widgets futuros | — | 6.3 | ✅ Completado |

---

## Fase 7 — Integración y Verificación

| # | Área | Tarea | Descripción | Depende de | Estado |
|---|------|-------|-------------|------------|--------|
| 7.1 | Full Stack | Prueba de flujo login completo | Usuario abre app → redirige a Azure → regresa con token → guard verifica registro → entra al dashboard | 5.x, 3.x, 6.x | ⬜ Pendiente |
| 7.2 | Full Stack | Prueba usuario no registrado | Login exitoso en Azure → guard detecta 404 en `/api/auth/me` → redirige a `/acceso-denegado` → envía solicitud → Admin la ve en panel | 5.7, 3.16 | ⬜ Pendiente |
| 7.3 | Full Stack | Prueba de roles | Usuario con rol `administrador` ve menú completo; usuario con rol `usuario` ve solo sus opciones | 5.2, 6.4 | ⬜ Pendiente |
| 7.4 | Backend | Prueba health checks | `GET /health-check`, `/health-details`, `/health/live`, `/health/ready` responden correctamente | 2.8 | ⬜ Pendiente |
| 7.5 | Full Stack | Verificar CORS | Token adjunto correctamente; sin errores de CORS en browser; header `X-Pagination` accesible | 2.4, 5.8 | ⬜ Pendiente |

---

## Dependencias npm (Fase 4.2)

```bash
# Auth
npm install angular-auth-oidc-client

# i18n
npm install @jsverse/transloco

# Tailwind CSS
npm install -D tailwindcss@^3.4 postcss autoprefixer

# Testing
npm install -D vitest @vitest/coverage-v8 jsdom @testing-library/angular
```

## Paquetes NuGet Backend

```bash
# Bisoft internos (feed Azure DevOps)
Bisoft.DatabaseConnections
Bisoft.Exceptions
Bisoft.Logging.Util

# Autenticación Entra ID
Microsoft.AspNetCore.Authentication.JwtBearer

# API Versioning
Asp.Versioning.Http

# Swagger
Swashbuckle.AspNetCore
Microsoft.AspNetCore.OpenApi

# Health Checks
AspNetCore.HealthChecks.UI.Client

# EF Core
Microsoft.EntityFrameworkCore.SqlServer

# Rate Limiting
Microsoft.AspNetCore.RateLimiting
```

---

## Leyenda de Estados

| Ícono | Estado |
|-------|--------|
| ⬜ | Pendiente |
| 🔄 | En progreso |
| ✅ | Completado |
| 🔴 | Bloqueado |
