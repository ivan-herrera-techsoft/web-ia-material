# CHECKLIST.md — Verificación Pre-Commit

> Claude debe verificar estos puntos antes de dar por completada cualquier tarea.
> Usar como complemento del comando `/review`.

---

## Componente Nuevo

- [ ] `standalone: true` — nunca NgModules
- [ ] `changeDetection: ChangeDetectionStrategy.OnPush` — nunca Default
- [ ] `input()` / `output()` signals — nunca `@Input()` / `@Output()`
- [ ] Booleans: `input(false, { transform: booleanAttribute })`
- [ ] Máximo **150 líneas** por componente
- [ ] Selector con prefijo `app-` + kebab-case
- [ ] Archivo en kebab-case sin sufijo de tipo (ej. `cliente-card.ts`, no `cliente-card.component.ts`)

### HTML Semántico
- [ ] `<button>` para acciones — nunca `<div>`/`<span>` con `(click)`
- [ ] `<a>` para navegación — nunca `<button>` ni `<div>`
- [ ] `type="button"` explícito en botones dentro de `<form>`
- [ ] `<label for="id">` asociado a cada `<input id="id">` — nunca placeholder como sustituto
- [ ] Un solo `<h1>` por página, jerarquía sin saltar niveles
- [ ] `<table>` solo para datos tabulares con `<thead>`, `<th scope>`, `<caption>`
- [ ] `<fieldset>` + `<legend>` para grupos de formulario
- [ ] ARIA solo cuando HTML nativo no alcanza — nunca redundante
- [ ] `alt` descriptivo en imágenes informativas; `alt=""` en decorativas

### Estilos
- [ ] **Cero colores directos de Tailwind** — solo tokens semánticos (`bg-primary`, `text-text-secondary`)
- [ ] Sin CSS inline ni `styleUrls`
- [ ] Sin media queries manuales — breakpoints de Tailwind mobile-first
- [ ] Componentes UI consumen variantes del objeto `THEME` vía `[ngClass]`

### Internacionalización
- [ ] **Cero strings hardcodeados** en UI — todo por `| transloco`
- [ ] Claves agregadas en `es.json` y `en.json`
- [ ] Máximo 3 niveles de profundidad en claves
- [ ] JSON plano en archivos de traducción
- [ ] Pipe `| transloco` — nunca directiva `*transloco`
- [ ] Dumb components: importar `TranslocoModule`, nunca inyectar `TranslocoService`

### Arquitectura
- [ ] **Dumb components:** solo `input()` / `output()` — sin inyección de servicios de negocio
- [ ] **Smart components (páginas):** inyectan servicios, leen signals, delegan UI a dumb components
- [ ] Sin lógica de negocio en componentes — encapsulada en servicios
- [ ] Sin HTTP directo en componentes — delegar al servicio

---

## Servicio Nuevo

- [ ] **Preguntó al usuario antes de crearlo** — regla de aggregate root
- [ ] Un servicio por aggregate root — sub-entidades se gestionan desde el servicio raíz
- [ ] Signal privado: `private readonly _items = signal<T[]>([])`
- [ ] API pública: `readonly items = this._items.asReadonly()`
- [ ] Estado derivado: `computed()` — en el servicio, nunca en componentes
- [ ] `effect()` solo en servicios — si necesita mutar signal, preferir `computed()`
- [ ] Genéricos tipados: `signal<Cliente[]>([])`, `http.get<Cliente[]>()`
- [ ] Inmutabilidad: `signal.update(list => [...list, nuevo])` — nunca mutación directa
- [ ] Mutaciones HTTP: `tap(() => this.cargar())` para sincronizar estado
- [ ] Errores: `err.error?.message ?? err.statusText ?? 'Error desconocido'` (son `HttpErrorResponse`)
- [ ] `takeUntilDestroyed(this.destroyRef)` con `DestroyRef` inyectado como campo — en métodos de evento
- [ ] Sin `any` — usar `unknown` + type guards para datos externos
- [ ] Sin acceso directo a `window`/`document`/`sessionStorage`/`localStorage` — usar adapters

---

## Guard Nuevo

- [ ] **Funcional** — nunca clase con `CanActivate`
- [ ] Síncrono (`boolean | UrlTree`) cuando datos ya están disponibles
- [ ] Asíncrono (`Observable<boolean | UrlTree>`) cuando signal arranca en `null`
- [ ] Si asíncrono: `toObservable() + filter() + take(1)` — nunca leer signal síncronamente
- [ ] Patrón mínimo necesario — no sobrecomplicar

---

## Formulario Nuevo

- [ ] `ReactiveFormsModule` con `FormBuilder` — nunca Template-driven para lógica compleja
- [ ] Validadores tipados
- [ ] `form.markAllAsTouched()` antes de mostrar errores
- [ ] Signal para mensaje de error: `readonly error = signal<string | null>(null)`
- [ ] `[attr.aria-invalid]` en inputs con errores
- [ ] `<fieldset>` + `<legend>` agrupando campos relacionados

---

## Test Nuevo

- [ ] Archivo `.spec.ts` junto al archivo fuente
- [ ] Standalone components: `imports: [Component]` en `configureTestingModule`
- [ ] `fixture.componentRef.setInput()` para establecer `input()` signals
- [ ] `fixture.detectChanges()` después de cambiar inputs
- [ ] `TestBed.runInInjectionContext()` para leer signals en contexto de inyección
- [ ] Mocks con `vi.fn()` — nunca `jasmine.createSpy()`
- [ ] HTTP tests: `provideHttpClient()` + `provideHttpClientTesting()` + `HttpTestingController`
- [ ] Guards asíncronos: `firstValueFrom()` para resolver Observable
- [ ] `afterEach(() => httpTesting.verify())` en tests con HTTP

---

## Traducciones

- [ ] Claves en ambos idiomas: `es.json` y `en.json`
- [ ] Estructura: `modulo.seccion.clave` (máx 3 niveles)
- [ ] JSON plano — sin arrays ni objetos como valores de hoja
- [ ] Reutilizar `common.*` para acciones genéricas
- [ ] Scoped translations solo para features lazy-loaded con `provideTranslocoScope()`

---

## Autenticación

- [ ] Solo `angular-auth-oidc-client` — nunca MSAL ni `angular-oauth2-oidc`
- [ ] `responseType: 'code'` (PKCE) — nunca implicit
- [ ] Tokens en `sessionStorage` — nunca `localStorage`
- [ ] `secureRoutes` solo con URLs de API propia — nunca `'*'`
- [ ] Claims validados con `Array.isArray()` antes de cast
- [ ] Acceso a OIDC solo vía `AuthService` — nunca `OidcSecurityService` directamente
- [ ] Variables `issuer` y `clientId` desde `environment.ts` — nunca hardcodeadas

---

## TypeScript

- [ ] `strict: true` en tsconfig
- [ ] Tipos explícitos en parámetros, retorno y variables no triviales
- [ ] Sin `any` — usar `unknown` + type guards
- [ ] Sin castings `as Tipo` sin validación — usar type guards
- [ ] Utility types (`Omit`, `Partial`, `Pick`, `Readonly`) en lugar de interfaces duplicadas
- [ ] Uniones de strings o `as const` en lugar de enums numéricos
- [ ] Métodos de máximo **20-30 líneas**

---

## Git

- [ ] Conventional Commits: `feat:`, `fix:`, `refactor:`, `chore:`
- [ ] Mensaje claro y en imperativo
