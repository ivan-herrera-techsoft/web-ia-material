# CLAUDE.md — Estándares de Proyecto

> Este archivo define las convenciones, arquitectura y librerías estándar para el proyecto.
> Claude debe seguir estas reglas sin excepción salvo que el usuario indique lo contrario explícitamente.
> Este proyecto solo contempla frontend.

---

> **Archivos complementarios — Claude debe consultarlos según el contexto:**
>
> - `DESIGN-TOKENS.md` — Paleta completa de tokens CSS, colores light/dark y mapeo a clases Tailwind
> - `UI-COMPONENTS.md` — Catálogo de componentes del design system y objeto THEME completo
> - `PATTERNS.md` — Patrones de implementación por caso de uso (CRUD, servicios, guards, adapters)
> - `ANGULAR-COMMANDS.md` — Comandos disponibles para scaffolding y revisión (/new-feature, /review, etc.)
> - `STYLES-GUIDE.md` — Guía de layouts de página, spacing, tipografía y responsive
> - `CHECKLIST.md` — Checklist de verificación pre-commit por tipo de artefacto
> - `CLAUDE-REFERENCIA.md` — Implementaciones de referencia completas y material educativo

---

## FRONTEND — Angular

### Stack y Librerías

| Propósito     | Librería / Tecnología                                                                                                                                         |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Framework     | Angular **21.2.1** (Standalone Components)                                                                                                                    |
| Lenguaje      | TypeScript estricto (`strict: true` en tsconfig)                                                                                                              |
| Estilos       | Tailwind CSS **v3.4** (utility-first, sin CSS custom salvo excepciones)                                                                                       |
| Design System | Objeto `THEME` en `theme/theme.ts` — clases Tailwind por variante usando tokens semánticos de CSS variables. Ver `UI-COMPONENTS.md` para el catálogo completo |
| Estado global | Angular Signals (`signal()`, `computed()`, `effect()`) — **sin NgRx**                                                                                         |
| HTTP Client   | Angular `HttpClient` con `withInterceptorsFromDi()`                                                                                                           |
| Formularios   | Reactive Forms (`FormBuilder`) — nunca Template-driven en lógica compleja                                                                                     |
| Validaciones  | Validators personalizados con tipado fuerte                                                                                                                   |
| Auth          | `angular-auth-oidc-client` (OAuth2/OIDC — multi-proveedor)                                                                                                    |
| i18n          | `@jsverse/transloco` (internacionalización runtime con lazy loading y type-safe keys)                                                                         |
| Routing       | Angular Router — Lazy Loading para rutas de Revocaciones y futuras features                                                                                   |
| Testing       | **Vitest 4.x** + jsdom                                                                                                                                        |
| Iconos        | SVG inline en templates                                                                                                                                       |
| Plataformas   | Browser + PWA — Patrón Adapter con `InjectionToken` + `PLATFORM_ID` + `afterNextRender()`                                                                     |

### Estructura de Carpetas Angular

```
src/
├── assets/
│   └── i18n/                    # Archivos JSON de traducción globales
│       ├── es.json              # Español — idioma base del proyecto
│       └── en.json              # Inglés
src/app/
├── app.ts               # Componente raíz (RouterOutlet + Sidebar + Header + init OIDC)
├── app.config.ts        # ApplicationConfig: provideRouter + providers DI + provideAuth + provideTransloco
├── app.routes.ts        # Tabla de rutas SPA (protegidas con autoLoginPartialRoutesGuard)
├── auth/                # Configuración OIDC, guards, AuthService
│   ├── auth.config.ts   # Configuración de angular-auth-oidc-client (issuer, scopes, etc.)
│   ├── auth.service.ts  # Roles, permisos por entidad, usuario autenticado
│   ├── permission.guard.ts       # Factory guard funcional (permissionGuard)
│   └── user-registered.guard.ts  # Guard: verifica que el usuario existe en el sistema
├── theme/               # THEME object y tipos (ButtonVariant, BadgeVariant, etc.)
├── shared/
│   ├── ui/              # Componentes UI reutilizables (AppButton, AppBadge, AppCard…)
│   ├── utils/           # Utilidades puras (ej. contrastTextColor)
│   ├── platform/        # Adapters de plataforma con InjectionToken (Browser / PWA)
│   │   ├── storage.token.ts     # InjectionToken<StorageAdapter> — abstracción de storage
│   │   ├── storage.browser.ts   # Implementación sessionStorage (browser)
│   │   ├── storage.pwa.ts       # Implementación con fallback offline (PWA/SW)
│   │   ├── clipboard.token.ts   # InjectionToken<ClipboardAdapter>
│   │   └── clipboard.browser.ts # Implementación Clipboard API
│   └── i18n/            # OPCIONAL — solo para features lazy-loaded con provideTranslocoScope()
│       └── [feature]/
│           ├── es.json
│           └── en.json
├── models/              # Interfaces TypeScript de dominio (globales a toda la app)
├── repositories/        # Patrón repositorio: interfaz + InjectionToken + implementaciones
├── services/            # Servicios de negocio con signals (uno por aggregate root)
├── pages/               # Páginas de la SPA agrupadas por feature
│   └── [feature]/
│       ├── [feature]-lista.ts       # Listado con filtros
│       ├── [feature]-detalle.ts     # Vista de detalle
│       └── [feature]-formulario.ts  # Alta / edición (distingue modo por route param)
├── sidebar/             # Navegación lateral colapsable
└── header/              # Barra superior (usuario, notificaciones, logout)
```

### Standalone Components (obligatorio)

Siempre crear componentes standalone. Nunca usar NgModules.

```typescript
// ✅ Patrón mínimo de componente
@Component({
  selector: "app-cliente-card",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./cliente-card.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClienteCardComponent {
  cliente = input.required<Cliente>();
}
```

**Reglas de componentes:**

- Siempre `ChangeDetectionStrategy.OnPush`
- Usar `input()` / `output()` signals — nunca `@Input()` / `@Output()`
- Componentes de presentación (dumb): solo `input()` y `output()`, sin inyección de servicios de negocio
- Componentes de página (smart): inyectan servicios, leen signals, delegan UI a componentes dumb
- Máximo 150 líneas por componente
- **Antes de crear un componente UI**, verificar si ya existe en `shared/ui/` — ver `UI-COMPONENTS.md`

### Tailwind CSS — Convenciones

**Tokens semánticos obligatorios — nunca colores directos de Tailwind en componentes:**

```html
<!-- ✅ CORRECTO -->
<div class="bg-bg-base border border-border-default text-text-primary">
  <button class="bg-primary text-primary-text hover:bg-primary-hover">
    Acción
  </button>
</div>

<!-- ❌ INCORRECTO — colores directos -->
<div class="bg-white border border-slate-200 text-slate-900">
  <button class="bg-sky-600 text-white hover:bg-sky-700">Acción</button>
</div>
```

> **Referencia completa de tokens:** ver `DESIGN-TOKENS.md` para la paleta de colores, mapeo CSS variables → clases Tailwind y combinaciones correctas por contexto.

**Reglas:**

- Clases utilitarias de Tailwind para layout, spacing y tipografía — nunca CSS inline ni `styleUrls`
- Colores siempre vía tokens semánticos (`bg-primary`, `text-text-secondary`) — definidos en CSS variables y mapeados en `tailwind.config.js`
- Patrones repetitivos: extraer con `@apply` en `styles/components.css`
- CSS puro: usar `theme()` en lugar de valores hardcodeados
- Responsividad: breakpoints de Tailwind mobile-first — nunca media queries manuales
- Dark mode: controlado por atributo `data-theme` — no por clase

**Design System interno (sin Angular Material):**

- Objeto `THEME` en `theme/theme.ts` es la fuente única de verdad de estilos — ver `UI-COMPONENTS.md`
- Los componentes consumen variantes del `THEME`, nunca clases de color directas
- Componentes en `shared/ui/`: `<app-button>`, `<app-badge>`, `<app-card>`, `<app-select>`, `<app-tabs>`
- Tipos derivados: `ButtonVariant`, `BadgeVariant`, `CardPadding`, `InputVariant`

### HTML Semántico

- Usar elementos nativos con semántica correcta — priorizar sobre ARIA
- `<button>` para acciones, `<a>` para navegación — nunca `<div>` / `<span>` con `(click)`
- `type="button"` explícito en botones dentro de `<form>` (default es `submit`)
- `<label>` asociado a cada `<input>` con `for`/`id` — nunca `placeholder` como sustituto
- Un solo `<h1>` por página; jerarquía sin saltar niveles
- `<table>` solo para datos tabulares con `<thead>`, `<th scope>`, `<caption>`
- Landmarks: `<header>`, `<nav>`, `<main>`, `<aside>`, `<footer>`
- `<fieldset>` + `<legend>` para grupos de formulario
- ARIA solo cuando el HTML nativo no es suficiente — nunca redundante (`role="button"` en `<button>`)
- `alt` descriptivo en imágenes informativas; `alt=""` en decorativas

### Lazy Loading de Rutas

La mayoría de rutas usan `component:` (eager). Usar `loadComponent()` solo para features grandes o poco frecuentes.

### Guard Clauses — Route Guards

- Siempre **functional guards** — nunca clases con `CanActivate`
- Guard síncrono (`boolean | UrlTree`): cuando los datos ya están disponibles (signals inicializados, roles de `AuthService`)
- Guard asíncrono (`Observable<boolean | UrlTree>`): cuando el signal arranca en `null` y se popula vía `subscribe()` — usar `toObservable() + filter() + take(1)`
- Elegir el patrón mínimo necesario
- Ver `PATTERNS.md` para implementaciones de referencia de ambos tipos

### Autenticación — OAuth2/OIDC

- Librería: `angular-auth-oidc-client` — nunca MSAL, nunca `angular-oauth2-oidc`
- Flujo: Authorization Code + PKCE (`responseType: 'code'`) — nunca implicit
- `AuthService` es la fuente única de acceso a identidad y roles — nunca acceder a `OidcSecurityService` directamente
- Tokens en `sessionStorage` (default) — nunca `localStorage`
- `secureRoutes` solo con URLs de la API propia — nunca `'*'`
- Validar claims con `Array.isArray()` antes de cast
- Variables `issuer` y `clientId` siempre desde `environment.ts` — nunca hardcodeadas

### Internacionalización — @jsverse/transloco

- Librería única: `@jsverse/transloco` — nunca `@angular/localize` ni `ngx-translate`
- Pipe `| transloco` en templates — forma preferida
- Servicios: `selectTranslate()` + `toSignal()` para traducciones pre-computadas; `translate()` solo en métodos disparados por acciones del usuario
- Scoped translations con `provideTranslocoScope()` solo para features lazy-loaded
- Archivos JSON globales en `src/assets/i18n/`; scoped opcionales en `shared/i18n/[feature]/`
- Máximo 3 niveles de profundidad en claves (`modulo.seccion.clave`)
- Nunca usar la directiva estructural `*transloco` — incompatible con `@if`/`@for`

### Soporte Multi-Plataforma — Browser + PWA

**Tres mecanismos:**

| Mecanismo                                 | Cuándo usarlo                                                       |
| ----------------------------------------- | ------------------------------------------------------------------- |
| **Patrón Adapter + `InjectionToken`**     | APIs de plataforma en servicios: storage, clipboard, notificaciones |
| **`PLATFORM_ID` + `isPlatformBrowser()`** | Solo en `app.config.ts` para registrar el adapter correcto          |
| **`afterNextRender()`**                   | Acceso al DOM desde componentes (canvas, gráficos, librerías DOM)   |

**Reglas:**

- Nunca acceder a `window`, `document`, `sessionStorage`, `localStorage` directamente en servicios — usar `inject(STORAGE_ADAPTER)` o el adapter correspondiente
- `PLATFORM_ID` solo en factories de `app.config.ts` — nunca en servicios ni componentes
- `afterNextRender()` para acceso al DOM — nunca en constructores ni `ngOnInit`
- `viewChild.required<ElementRef<T>>('ref')` en lugar de `@ViewChild()` — acceder a `.nativeElement` solo dentro de `afterNextRender()`
- Adapters centralizados en `shared/platform/` — ver `PATTERNS.md` para el patrón completo
- Service Worker solo en producción (`environment.production`)

> **Excepción:** el script pre-Angular en `index.html` accede a `localStorage` directamente para inicializar el tema (evitar FOUC).

### Gestión de Estado — Angular Signals

**Patrón de servicio:**

```typescript
@Injectable({ providedIn: "root" })
export class ClientesService {
  private readonly _clientes = signal<Cliente[]>([]); // privado, mutable
  readonly clientes = this._clientes.asReadonly(); // público, readonly
  readonly regionesUsadas = computed(() =>
    // derivado
    [...new Set(this._clientes().map((c) => c.region))],
  );
}
```

> **Patrones completos** de servicio con HTTP y en memoria: ver `PATTERNS.md`.

**Reglas:**

- Estado mutable: `signal<T>()` — siempre privado
- API pública: `.asReadonly()`
- Estado derivado: `computed()` — en el servicio, nunca en componentes
- `effect()` solo en servicios — si necesita mutar un signal, preferir `computed()`
- `input<T>()` en lugar de `@Input()`; booleans: `input(false, { transform: booleanAttribute })`
- Inmutabilidad: siempre `signal.update(list => [...list, nuevo])`

### HTTP y Servicios

**Reglas:**

- Un servicio por **aggregate root** — sub-entidades y value objects se gestionan desde el servicio raíz. **Preguntar al usuario antes de crear cualquier servicio nuevo**
- Dividir servicios grandes por **concern** (CRUD, estadísticas, workflow), nunca por sub-entidad
- El servicio es el único dueño del signal — tras mutaciones usa `tap(() => this.cargar())` para sincronizar
- Los componentes se suscriben al Observable de la mutación solo para reaccionar (navegar, error, cerrar modal)
- `cargar()` se llama en el constructor del componente de página
- Siempre tipar el genérico de `HttpClient`: `http.get<Cliente[]>()`
- `takeUntilDestroyed(this.destroyRef)` con `DestroyRef` inyectado como campo de clase — en métodos de evento no hay injection context
- Errores HTTP: `err.error?.message ?? err.statusText ?? 'Error desconocido'` (son `HttpErrorResponse`, no `Error`)

### Testing — Vitest + jsdom

**Reglas:**

- Un archivo `.spec.ts` por clase, junto al archivo fuente
- Standalone components: importar directamente en `imports: []` del `configureTestingModule`
- `fixture.componentRef.setInput()` para establecer `input()` signals
- `TestBed.runInInjectionContext()` para leer signals en contexto de inyección
- Mocks con `vi.fn()` — nunca `jasmine.createSpy()`
- `fixture.detectChanges()` después de cambiar inputs antes de aserciones sobre el DOM
- HTTP tests: `provideHttpClient()` + `provideHttpClientTesting()` + `HttpTestingController`
- Guards asíncronos: `firstValueFrom()` para resolver el Observable en tests

---

## CONVENCIONES GENERALES DE CÓDIGO

### Nombrado

- **Clases / Interfaces / Types:** PascalCase
- **Variables / Funciones / Métodos:** camelCase
- **Constantes:** UPPER_SNAKE_CASE
- **Archivos Angular:** kebab-case sin sufijo de tipo (`clientes-lista.ts`, `auth.service.ts`)
- **Selectores Angular:** prefijo `app-` + kebab-case
- **Carpetas:** kebab-case

### TypeScript estricto — Tipado explícito

- Tipos explícitos en parámetros, retorno y variables no triviales
- Inferencia aceptable solo cuando el tipo es evidente en la misma línea (`const nombre = 'Acme'`)
- `any` prohibido — usar `unknown` + type guards para datos externos. Excepción: interop con librerías sin tipos o tests
- Type guards (`valor is Tipo`) en lugar de castings `as Tipo`
- Utility types (`Omit`, `Partial`, `Pick`, `Readonly`) en lugar de interfaces duplicadas
- Uniones de strings o `as const` en lugar de enums numéricos
- Siempre tipar genéricos: `signal<Cliente[]>([])`, `http.get<Cliente[]>()`

### Principios

- **SOLID** en lógica de negocio
- **Fail fast** con guard clauses
- **Inmutabilidad** por defecto
- **No magic strings** — constantes o enums tipados
- Métodos de máximo **20-30 líneas**

### Git y Commits

Conventional Commits: `feat:`, `fix:`, `refactor:`, `chore:`

---

## ENTORNO DE DESARROLLO

### Comandos principales

```bash
npm start          # ng serve → http://localhost:4200
npm run build      # ng build — build de producción
npm run watch      # ng build --watch --configuration development
npm test           # Vitest — suite de pruebas
```

---

## FLUJO DE TRABAJO CON CLAUDE CODE

### Al crear una feature nueva

1. Consultar `PATTERNS.md` para la estructura de archivos y patrones a seguir
2. Consultar `UI-COMPONENTS.md` para verificar componentes reutilizables existentes
3. Consultar `DESIGN-TOKENS.md` para los tokens de color y spacing correctos
4. Consultar `STYLES-GUIDE.md` para el layout de página apropiado
5. Antes de dar por completada la tarea, verificar contra `CHECKLIST.md`

### Comandos disponibles

Ver `COMMANDS.md` para la lista completa de comandos:

- `/new-feature`, `/new-component`, `/new-service`, `/new-guard`, `/new-adapter`
- `/add-translations`, `/add-test`
- `/review`, `/refactor`

---

## LO QUE CLAUDE NO DEBE HACER

### TypeScript estricto

- ❌ No usar `any` — usar `unknown` + type guards
- ❌ No omitir tipos de parámetros ni retorno en servicios/guards/funciones de negocio
- ❌ No omitir genéricos en `signal<T>()`, `computed<T>()`, `http.get<T>()`
- ❌ No usar casting `as Tipo` sin validación — usar type guards
- ❌ No duplicar interfaces — usar utility types
- ❌ No usar enums numéricos — usar string unions o `as const`
- ❌ No inicializar arrays sin tipo: `const items: Cliente[] = []`

### Arquitectura y componentes

- ❌ No crear lógica de negocio en componentes — encapsularla en servicios
- ❌ No usar NgModules — solo Standalone Components
- ❌ No inyectar servicios de negocio en dumb components
- ❌ No usar `ChangeDetectionStrategy.Default` — siempre `OnPush`
- ❌ No crear guards como clases — usar functional guards
- ❌ No leer signals de AuthService síncronamente en guards — usar `toObservable().pipe(filter(), take(1))`
- ❌ No hacer HTTP directamente en componentes
- ❌ No crear servicios para sub-entidades — gestionarlas desde el aggregate root
- ❌ No dividir servicios por sub-entidad — dividir por concern
- ❌ No crear componentes UI sin verificar primero `UI-COMPONENTS.md`

### Estado reactivo

- ❌ No usar NgRx
- ❌ No exponer signals mutables — usar `.asReadonly()`
- ❌ No mutar estado directamente — usar `signal.update()`
- ❌ No calcular estado derivado en componentes — usar `computed()` en servicios
- ❌ No usar `.subscribe()` sin desuscripción — usar `toSignal()` o `takeUntilDestroyed(this.destroyRef)`
- ❌ No usar `takeUntilDestroyed()` sin argumento en métodos de evento
- ❌ No acceder a `err.message` en errores HTTP — usar `err.error?.message ?? err.statusText`

### Formularios

- ❌ No usar Template-driven forms para lógica compleja — Reactive Forms con `FormBuilder`

### Autenticación

- ❌ No usar MSAL ni `angular-oauth2-oidc`
- ❌ No usar implicit flow — solo PKCE
- ❌ No almacenar tokens en `localStorage`
- ❌ No configurar `secureRoutes: ['*']`
- ❌ No leer claims sin validar con `Array.isArray()`
- ❌ No acceder a `OidcSecurityService` fuera de `AuthService`

### Internacionalización

- ❌ No usar strings hardcodeados — todo por `| transloco`
- ❌ No usar `@angular/localize` ni `ngx-translate`
- ❌ No inyectar `TranslocoService` en dumb components
- ❌ No crear traducciones que no sean JSON plano
- ❌ No usar más de 3 niveles de profundidad en claves
- ❌ No usar `*transloco` — usar pipe `| transloco`

### HTML Semántico

- ❌ No usar `<div>`/`<span>` con `(click)` — usar `<button>` o `<a>`
- ❌ No omitir `type="button"` en botones dentro de `<form>`
- ❌ No usar `placeholder` como sustituto de `<label>`
- ❌ No omitir `for`/`id` en `<label>`+`<input>`
- ❌ No saltar niveles de encabezados ni usar más de un `<h1>`
- ❌ No usar `<table>` para layout
- ❌ No agregar ARIA redundante sobre semántica nativa
- ❌ No omitir `alt` en imágenes

### Estilos y Design System

- ❌ No usar colores directos de Tailwind — solo tokens semánticos (ver `DESIGN-TOKENS.md`)
- ❌ No crear clases de color hardcodeadas — consumir variantes del objeto `THEME`
- ❌ No usar CSS inline ni `styleUrls`
- ❌ No usar media queries manuales — breakpoints de Tailwind mobile-first
- ❌ No usar Angular Material — design system propio basado en `THEME`

### Soporte Multi-Plataforma

- ❌ No acceder a `window`/`document`/`sessionStorage`/`localStorage` directamente en servicios — usar adapters
- ❌ No usar `typeof window !== 'undefined'` como guard — detección en `app.config.ts`
- ❌ No inyectar `PLATFORM_ID` en servicios ni componentes
- ❌ No acceder al DOM fuera de `afterNextRender()`
- ❌ No usar `@ViewChild()` — usar `viewChild.required<ElementRef<T>>()`
- ❌ No crear adapters fuera de `shared/platform/`
- ❌ No habilitar Service Worker en desarrollo

## Backend .NET 10 / C#

## Comandos

```bash
dotnet restore        # Restaurar dependencias (incluye feed privado de Azure DevOps Artifacts)
dotnet build          # Compilar el proyecto
dotnet run            # Ejecutar el proyecto
dotnet test           # Ejecutar pruebas
```

Feed NuGet privado: Azure DevOps Artifacts (configurado en `nuget.config`).

Paquetes internos Bisoft disponibles:

- `Bisoft.DatabaseConnections` — multi-proveedor BD (SqlServer, PostgreSQL, SQLite)
- `Bisoft.Exceptions` — excepciones tipadas con códigos y localización
- `Bisoft.AutomatedServices` — base para Background Services temporizados
- `Bisoft.NotificationBus` — notificaciones multi-canal (Email, Telegram, SMS, etc.)
- `Bisoft.EventManager` — gestión de eventos con lifecycle y reintentos
- `Bisoft.Security.RefreshTokens.EntityFramework` — Refresh Tokens para JWT

## Tecnología

- .NET 10, C# — Minimal API (no usar Controllers)
- Nullable reference types habilitado (`<Nullable>enable</Nullable>`)
- Arquitectura en capas: **Hermes** (pequeño), **Atenea** (microservicio), **Titan** (gran escala) — ver `claude/rules/architectures.md`
- Database First: el esquema se gestiona externamente, el modelo se genera desde la BD

## Estructura de directorios (Atenea)

```
Company.Product.Module/
├── Api/
│   ├── Endpoints/
│   ├── BackgroundServices/
│   ├── Extensions/
│   │   └── Configuration/
│   ├── Dtos/
│   │   └── Configurations/
│   ├── HealthChecks/
│   └── Middleware/
├── Application/
│   ├── Dtos/
│   └── Services/
├── Domain/
│   ├── Contracts/Repositories/
│   ├── Entities/
│   ├── Exceptions/
│   ├── Services/
│   └── Validators/
├── Infrastructure/
│   ├── Contexts/
│   ├── Mapping/
│   ├── Repositories/
│   └── Strategies/
├── Shared/
└── Test/
```

## Idioma del código

- **Negocio en español**: entidades, métodos, variables, parámetros de dominio
- **Sufijos técnicos en inglés**: `Repository`, `Service`, `DomainService`, `Strategy`, `Middleware`, `Configuration`, `BackgroundService`, `Endpoint`, `Validator`, `Context`, `Mapping`
- **Carpetas técnicas en inglés**: `Repositories/`, `Services/`, `Entities/`, `Contexts/`
- **Sin sufijo Async** en nombres de métodos aunque el método sea `async Task`

### Naming por elemento

| Elemento            | Correcto                 | Incorrecto             |
| ------------------- | ------------------------ | ---------------------- |
| Clase entidad       | `Canal`                  | `Channel`              |
| Repositorio         | `CanalRepository`        | `ChannelRepository`    |
| Servicio dominio    | `CanalDomainService`     | `ChannelDomainService` |
| Servicio aplicación | `CanalService`           | `ChannelService`       |
| Campo privado       | `_repositorioCanal`      | `_channelRepo`         |
| DTO request         | `CrearCanalRequest`      | `CreateChannelRequest` |
| DTO response        | `ObtenerCanalesResponse` | `GetChannelsResponse`  |

### Naming de métodos por capa

| Capa           | Operación         | Patrón                              | Ejemplo                     |
| -------------- | ----------------- | ----------------------------------- | --------------------------- |
| Application    | Listado           | `Obtener{Entidad}s`                 | `ObtenerCanales()`          |
| Application    | Detalle           | `ObtenerPorId`                      | `ObtenerPorId(id)`          |
| Application    | Crear             | `Guardar`                           | `Guardar(solicitud)`        |
| Application    | Actualizar        | `Actualizar`                        | `Actualizar(id, solicitud)` |
| Application    | Eliminar / Toggle | `Eliminar` / `Alternar`             | `Eliminar(id)`              |
| Domain Service | IQueryable        | `Consultar{Entidad}s`               | `ConsultarCanales()`        |
| Domain Service | Obtener / Guardar | `ObtenerPorId` / `Guardar`          | `ObtenerPorId(id)`          |
| Infrastructure | IQueryable        | `Consultar{Entidad}s`               | `ConsultarCanales()`        |
| Infrastructure | CRUD              | `Crear` / `Actualizar` / `Eliminar` | `Crear(canal)`              |

## Features de C# moderno

- **Constructor principal** en clases nuevas (excepto entidades de dominio)
- **Records** para DTOs inmutables sin herencia
- **File-scoped namespaces**: `namespace Company.Product.Module.Capa;`
- **Global usings**, **raw string literals**, **pattern matching**, **collection expressions** `[]`
- **ICollection / IEnumerable** en lugar de `List<T>` en propiedades y retornos

## Endpoints obligatorios

- `/health-check`, `/health-details`, `/health/live`, `/health/ready`
- Página de bienvenida en `/`
- Fechas en UTC (`DateTime.UtcNow`); conversión a local solo en presentación
- Reintentos con `Microsoft.Extensions.Http.Resilience` al consumir APIs externas
- Refresh Tokens obligatorio si se usa JWT — endpoint `/auth/refresh`

## Seguridad

En proyectos nuevos, **preguntar al desarrollador** qué esquema de autenticación usar: API Key, Cookies, JWT, OAuth 2.0/OIDC o Mixto.

Prácticas obligatorias: no hardcodear secretos (usar `SensitiveData` en appsettings), nunca `AllowAnyOrigin` en producción, nunca exponer stack traces, rate limiting en endpoints públicos.

## Bootstrap de Program.cs

Orden estricto — ver detalles en `claude/rules/program-bootstrap.md`:

1. `TException.SetComponentPrefix("PREFIJO")` — **primera instrucción**
2. `builder.Configuration.SetEncryption()` — antes de `GetConfiguration()`
3. `var generalConfiguration = builder.Configuration.GetConfiguration()`
4. 14 servicios en orden exacto → pipeline en orden exacto
5. Nunca pasar `IConfiguration` directamente a `ConfigureXxx` — siempre `generalConfiguration`

## Referencia de reglas

Todas las convenciones detalladas están en `claude/rules/`:

| Área                      | Archivo                  |
| ------------------------- | ------------------------ |
| Entidades de dominio      | `entities.md`            |
| Validadores (Formatear\*) | `entities.md`            |
| FluentValidation          | `fluent-validation.md`   |
| Repositorios + Cached     | `repositories.md`        |
| Domain Services           | `domain-services.md`     |
| Application Services      | `services.md`            |
| EF Core Mapping           | `ef-core-mapping.md`     |
| DbContext + Strategies    | `context.md`             |
| Endpoints                 | `endpoints.md`           |
| Paginación                | `pagination.md`          |
| CORS                      | `cors.md`                |
| Logger (Serilog)          | `logger.md`              |
| Autenticación             | `authentication.md`      |
| Rate Limiting             | `rate-limiting.md`       |
| Health Checks             | `health-checks.md`       |
| Swagger                   | `swagger.md`             |
| Telemetría                | `telemetry.md`           |
| Background Services       | `background-services.md` |
| Outbox Pattern            | `outbox-pattern.md`      |
| Home Mapping              | `home-mapping.md`        |
| Shared Layer              | `shared-layer.md`        |
| Arquitecturas             | `architectures.md`       |
| Program.cs bootstrap      | `program-bootstrap.md`   |
| Testing                   | `testing.md`             |
| Git y commits             | `git-conventions.md`     |
