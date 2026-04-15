# COMMANDS.md — Comandos Personalizados para Claude Code

> Comandos predefinidos que Claude Code debe reconocer y ejecutar siguiendo los patrones del proyecto.
> Cada comando sigue estrictamente las convenciones de `CLAUDE.md` y los patrones de `PATTERNS.md`.

---

## /new-feature [nombre]

**Descripción:** Scaffolding completo de una feature CRUD.

**Pasos que Claude debe ejecutar:**

1. **Preguntar al usuario** qué campos tiene el modelo (nombre, tipo, requerido)
2. Crear `models/[nombre].ts` con la interfaz y tipos derivados (`FormData`, `Resumen`)
3. Crear `services/[nombre].service.ts` siguiendo el patrón de servicio con HTTP de `PATTERNS.md`
4. Crear `pages/[nombre]/[nombre]-lista.ts` + `.html` (página smart)
5. Crear `pages/[nombre]/[nombre]-detalle.ts` + `.html` (página smart)
6. Crear `pages/[nombre]/[nombre]-formulario.ts` + `.html` (página smart con Reactive Forms)
7. Agregar rutas en `app.routes.ts` (protegidas con guards si corresponde)
8. Agregar claves de traducción en `es.json` y `en.json`
9. Agregar enlace en el sidebar si corresponde

**Validaciones post-creación:**
- Todos los componentes son standalone con `OnPush`
- Todos los strings de UI usan `| transloco`
- Todos los colores usan tokens semánticos
- El servicio usa signals privados + `.asReadonly()`
- HTML semántico en todos los templates

---

## /new-component [nombre]

**Descripción:** Componente dumb reutilizable en `shared/ui/`.

**Pasos que Claude debe ejecutar:**

1. **Preguntar al usuario** qué inputs y outputs necesita
2. Crear `shared/ui/[nombre].ts` como standalone component
3. Usar `input()` / `output()` signals — nunca decoradores
4. `ChangeDetectionStrategy.OnPush`
5. Si necesita variantes visuales, agregar al objeto `THEME` en `theme/theme.ts`
6. Documentar el componente en `UI-COMPONENTS.md`

**Template de archivo:**

```typescript
@Component({
  selector: 'app-[nombre]',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- template aquí -->
  `,
})
export class App[Nombre]Component {
  // inputs y outputs con signals
}
```

---

## /new-service [nombre]

**Descripción:** Servicio de negocio para un aggregate root.

**Antes de ejecutar:** preguntar al usuario si ya existe un servicio que debería manejar esta entidad (regla: un servicio por aggregate root).

**Pasos que Claude debe ejecutar:**

1. Verificar que no exista un servicio del mismo aggregate root
2. Crear `services/[nombre].service.ts` con el patrón de `PATTERNS.md`
3. Signal privado + `.asReadonly()` público
4. `computed()` para estado derivado
5. Métodos CRUD que retornan Observable con `tap(() => this.cargar())`

**Elegir patrón:**
- Con backend → Patrón de servicio con HTTP
- Sin backend (datos locales) → Patrón de servicio en memoria

---

## /new-guard [nombre]

**Descripción:** Guard funcional para protección de rutas.

**Pasos que Claude debe ejecutar:**

1. **Preguntar al usuario:** ¿el dato necesario para evaluar ya está disponible síncronamente o necesita esperar?
2. Si **síncrono** → guard que retorna `boolean | UrlTree`
3. Si **asíncrono** → guard que retorna `Observable<boolean | UrlTree>` con `toObservable() + filter() + take(1)`
4. Crear en `auth/[nombre].guard.ts`
5. Nunca crear como clase — siempre función o factory function

---

## /new-adapter [nombre]

**Descripción:** Adapter de plataforma con InjectionToken.

**Pasos que Claude debe ejecutar:**

1. Crear `shared/platform/[nombre].token.ts` con interfaz + `InjectionToken`
2. Crear `shared/platform/[nombre].browser.ts` con implementación para browser
3. Registrar en `app.config.ts`: `{ provide: [NOMBRE]_ADAPTER, useClass: Browser[Nombre]Adapter }`
4. Verificar que no accede a `window`/`document`/`sessionStorage`/`localStorage` directamente

---

## /add-translations [feature]

**Descripción:** Genera esqueleto de claves de traducción para una feature.

**Pasos que Claude debe ejecutar:**

1. Analizar los componentes de la feature para identificar strings de UI
2. Crear estructura de claves siguiendo `modulo.seccion.clave` (máx 3 niveles)
3. Agregar al `es.json` y `en.json` globales
4. Reutilizar claves de `common.*` cuando aplique
5. Verificar que ningún string de UI quede hardcodeado

**Claves comunes que ya deben existir en `common`:**

```json
{
  "common": {
    "guardar": "Guardar",
    "cancelar": "Cancelar",
    "eliminar": "Eliminar",
    "confirmar": "Confirmar",
    "cargando": "Cargando...",
    "error": "Ha ocurrido un error",
    "buscar": "Buscar...",
    "crear": "Crear nuevo",
    "editar": "Editar",
    "volver": "Volver",
    "si": "Sí",
    "no": "No"
  }
}
```

---

## /add-test [archivo]

**Descripción:** Genera archivo de test `.spec.ts` para un archivo existente.

**Pasos que Claude debe ejecutar:**

1. Analizar el archivo fuente para determinar el tipo (componente, servicio, guard, pipe)
2. Crear `[nombre].spec.ts` junto al archivo fuente
3. Seguir patrones de testing de `PATTERNS.md`:
   - Componentes: `TestBed.configureTestingModule({ imports: [Component] })` + `fixture.componentRef.setInput()`
   - Servicios: `TestBed.inject(Service)` + mock de `HttpClient` con `HttpTestingController`
   - Guards: `TestBed.runInInjectionContext()` + `firstValueFrom()`
4. Usar `vi.fn()` para mocks — nunca `jasmine.createSpy()`
5. `fixture.detectChanges()` después de cambiar inputs
6. Mínimo 2-3 tests por artefacto

---

## /review

**Descripción:** Revisa el archivo actual (o archivos indicados) contra las reglas de `CLAUDE.md`.

**Checklist que Claude debe verificar:**

**Componente:**
- [ ] Standalone con `ChangeDetectionStrategy.OnPush`
- [ ] `input()` / `output()` signals (no decoradores `@Input` / `@Output`)
- [ ] Máximo 150 líneas
- [ ] Sin colores directos de Tailwind — solo tokens semánticos
- [ ] Strings de UI por `| transloco`
- [ ] HTML semántico (`<button>`, `<label>`, `for`/`id`, `type="button"` en forms)
- [ ] Sin lógica de negocio en dumb components
- [ ] Sin `@ViewChild()` → usar `viewChild.required<ElementRef<T>>()`

**Servicio:**
- [ ] Signal privado + `.asReadonly()` público
- [ ] `computed()` para estado derivado
- [ ] Genéricos tipados: `signal<T>()`, `http.get<T>()`
- [ ] `takeUntilDestroyed(this.destroyRef)` en suscripciones de métodos de evento
- [ ] Errores: `err.error?.message ?? err.statusText ?? 'Error desconocido'`
- [ ] Sin `any` — usar `unknown` + type guards

**Guard:**
- [ ] Funcional (no clase)
- [ ] Patrón correcto (síncrono vs asíncrono)

**General:**
- [ ] Sin NgModules
- [ ] Sin NgRx
- [ ] Sin acceso directo a `window`/`document`/`sessionStorage`/`localStorage`
- [ ] Conventional Commits en el mensaje de commit

**Reportar** las violaciones encontradas con la línea, la regla violada y la corrección sugerida.

---

## /refactor [archivo]

**Descripción:** Refactoriza un archivo existente para alinearlo con las convenciones del proyecto.

**Pasos que Claude debe ejecutar:**

1. Ejecutar `/review` primero para identificar problemas
2. Aplicar correcciones en orden de prioridad:
   - Errores de tipado (any → unknown + type guards)
   - Decoradores legacy → signals (`@Input` → `input()`, `@Output` → `output()`)
   - Colores directos → tokens semánticos
   - Strings hardcodeados → claves de traducción
   - HTML no semántico → elementos nativos correctos
3. Verificar que los tests existentes siguen pasando
4. Si no hay tests, sugerir crearlos con `/add-test`
