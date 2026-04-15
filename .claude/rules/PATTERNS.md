# PATTERNS.md — Patrones de Implementación por Caso de Uso

> Templates estandarizados para cada tipo de artefacto del proyecto.
> Claude debe seguir estos patrones al generar código — son la estructura canónica.
> Reemplazar `[Feature]` / `[feature]` con el nombre real en PascalCase / kebab-case.

---

## 1. Patrón CRUD Completo — Nueva Feature

### Archivos a crear

```
src/app/
├── models/[feature].ts                          # Interfaz de dominio
├── services/[feature].service.ts                # Servicio con signals + HTTP
├── pages/[feature]/
│   ├── [feature]-lista.ts                       # Página lista (smart)
│   ├── [feature]-lista.html                     # Template lista
│   ├── [feature]-detalle.ts                     # Página detalle (smart)
│   ├── [feature]-detalle.html                   # Template detalle
│   ├── [feature]-formulario.ts                  # Página formulario (smart)
│   └── [feature]-formulario.html                # Template formulario
src/assets/i18n/
├── es.json                                      # Agregar claves de la feature
└── en.json                                      # Agregar claves de la feature
```

### Rutas a agregar en `app.routes.ts`

```typescript
// Dentro del children[] protegido
{ path: '[feature]', component: [Feature]Lista },
{ path: '[feature]/nuevo', component: [Feature]Formulario,
  canActivate: [permissionGuard(a => a.canManage[Feature]())] },
{ path: '[feature]/:id', component: [Feature]Detalle },
{ path: '[feature]/:id/editar', component: [Feature]Formulario,
  canActivate: [permissionGuard(a => a.canManage[Feature]())] },
```

---

## 2. Patrón de Modelo de Dominio

```typescript
// models/[feature].ts
export interface [Feature] {
  id: number;
  // ...campos de dominio tipados
  fechaCreacion: Date;
}

// Tipos derivados con utility types — nunca duplicar interfaces
export type [Feature]FormData = Omit<[Feature], 'id' | 'fechaCreacion'>;
export type [Feature]Resumen  = Pick<[Feature], 'id' | /* campos para listados */ >;
```

---

## 3. Patrón de Servicio con HTTP

```typescript
// services/[feature].service.ts
import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of, catchError, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { [Feature] } from '../models/[feature]';

@Injectable({ providedIn: 'root' })
export class [Feature]Service {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/[feature]`;

  // Estado — signal privado, readonly público
  private readonly _items = signal<[Feature][]>([]);
  private readonly _cargando = signal<boolean>(false);
  private readonly _errorRed = signal<boolean>(false);

  readonly items = this._items.asReadonly();
  readonly cargando = this._cargando.asReadonly();
  readonly errorRed = this._errorRed.asReadonly();

  // Estado derivado — siempre computed() en el servicio
  readonly total = computed(() => this._items().length);

  // Lectura
  cargar(): void {
    this._cargando.set(true);
    this._errorRed.set(false);
    this.http.get<[Feature][]>(this.apiUrl).pipe(
      catchError(() => { this._errorRed.set(true); return of([]); }),
    ).subscribe(data => {
      this._items.set(data);
      this._cargando.set(false);
    });
  }

  getById(id: number): [Feature] | undefined {
    return this._items().find(item => item.id === id);
  }

  // Mutaciones — retornan Observable para que el componente reaccione
  crear(data: Omit<[Feature], 'id' | 'fechaCreacion'>): Observable<[Feature]> {
    return this.http.post<[Feature]>(this.apiUrl, data).pipe(
      tap(() => this.cargar()),
    );
  }

  actualizar(id: number, changes: Partial<[Feature]>): Observable<[Feature]> {
    return this.http.put<[Feature]>(`${this.apiUrl}/${id}`, changes).pipe(
      tap(() => this.cargar()),
    );
  }

  eliminar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(
      tap(() => this.cargar()),
    );
  }
}
```

---

## 4. Patrón de Servicio en Memoria (sin backend)

```typescript
@Injectable({ providedIn: 'root' })
export class [Feature]Service {
  private readonly _items = signal<[Feature][]>([]);
  readonly items = this._items.asReadonly();

  readonly total = computed(() => this._items().length);

  getById(id: number): [Feature] | undefined {
    return this._items().find(item => item.id === id);
  }

  crear(data: Omit<[Feature], 'id' | 'fechaCreacion'>): void {
    const nuevo: [Feature] = {
      ...data,
      id: Date.now(),
      fechaCreacion: new Date(),
    };
    this._items.update(list => [...list, nuevo]);
  }

  actualizar(id: number, changes: Partial<[Feature]>): void {
    this._items.update(list =>
      list.map(item => item.id === id ? { ...item, ...changes } : item)
    );
  }

  eliminar(id: number): void {
    this._items.update(list => list.filter(item => item.id !== id));
  }
}
```

---

## 5. Patrón de Página Lista (Smart Component)

```typescript
// pages/[feature]/[feature]-lista.ts
import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { [Feature]Service } from '../../services/[feature].service';

@Component({
  selector: 'app-[feature]-lista',
  standalone: true,
  imports: [RouterLink, TranslocoModule, /* dumb components */],
  templateUrl: './[feature]-lista.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class [Feature]Lista {
  private readonly [feature]Service = inject([Feature]Service);

  // Exponer signals del servicio
  readonly items = this.[feature]Service.items;
  readonly cargando = this.[feature]Service.cargando;

  // Estado local de la página
  readonly filtro = signal('');

  // Estado derivado local
  readonly itemsFiltrados = computed(() =>
    this.items().filter(item =>
      /* lógica de filtrado */
      JSON.stringify(item).toLowerCase().includes(this.filtro().toLowerCase())
    )
  );

  constructor() {
    this.[feature]Service.cargar();
  }
}
```

### Template de página lista

```html
<!-- [feature]-lista.html -->
<section>
  <header class="mb-6 flex items-center justify-between">
    <div>
      <h1 class="text-2xl font-semibold text-text-primary">{{ '[feature].lista.titulo' | transloco }}</h1>
      <p class="mt-1 text-sm text-text-secondary">{{ '[feature].lista.descripcion' | transloco }}</p>
    </div>
    <a routerLink="nuevo" class="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-text shadow-sm transition-colors hover:bg-primary-hover">
      {{ 'common.crear' | transloco }}
    </a>
  </header>

  <!-- Filtro -->
  <div class="mb-4">
    <input
      type="text"
      [placeholder]="'common.buscar' | transloco"
      [value]="filtro()"
      (input)="filtro.set($any($event.target).value)"
      class="block w-full rounded-lg border border-border-default bg-bg-base px-3 py-2 text-sm text-text-primary placeholder:text-text-disabled focus:border-primary focus:ring-primary"
    />
  </div>

  <!-- Loading -->
  @if (cargando()) {
    <p class="py-8 text-center text-sm text-text-secondary">{{ 'common.cargando' | transloco }}</p>
  }

  <!-- Lista -->
  @if (!cargando()) {
    @for (item of itemsFiltrados(); track item.id) {
      <!-- Aquí usar dumb component: <app-[feature]-card [item]="item" /> -->
    } @empty {
      <p class="py-8 text-center text-sm text-text-secondary">{{ '[feature].lista.vacio' | transloco }}</p>
    }
  }
</section>
```

---

## 6. Patrón de Página Detalle (Smart Component)

```typescript
// pages/[feature]/[feature]-detalle.ts
@Component({
  selector: 'app-[feature]-detalle',
  standalone: true,
  imports: [RouterLink, TranslocoModule],
  templateUrl: './[feature]-detalle.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class [Feature]Detalle {
  private readonly route = inject(ActivatedRoute);
  private readonly [feature]Service = inject([Feature]Service);

  readonly item = computed(() => {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    return this.[feature]Service.getById(id);
  });
}
```

---

## 7. Patrón de Página Formulario (Smart Component)

```typescript
// pages/[feature]/[feature]-formulario.ts
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-[feature]-formulario',
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoModule, RouterLink],
  templateUrl: './[feature]-formulario.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class [Feature]Formulario {
  private readonly fb = inject(FormBuilder);
  private readonly [feature]Service = inject([Feature]Service);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly error = signal<string | null>(null);

  // Modo edición vs creación
  readonly id = computed(() => {
    const param = this.route.snapshot.paramMap.get('id');
    return param ? Number(param) : null;
  });
  readonly esEdicion = computed(() => this.id() !== null);

  // Formulario reactivo
  readonly form = this.fb.group({
    // campo: ['', [Validators.required]],
  });

  constructor() {
    // Pre-cargar datos en modo edición
    if (this.esEdicion()) {
      const item = this.[feature]Service.getById(this.id()!);
      if (item) {
        this.form.patchValue(item);
      }
    }
  }

  guardar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const data = this.form.getRawValue();
    const operacion = this.esEdicion()
      ? this.[feature]Service.actualizar(this.id()!, data)
      : this.[feature]Service.crear(data);

    operacion
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.router.navigate(['/[feature]']),
        error: (err: HttpErrorResponse) =>
          this.error.set(err.error?.message ?? err.statusText ?? 'Error desconocido'),
      });
  }
}
```

### Template de formulario

```html
<!-- [feature]-formulario.html -->
<section>
  <h1 class="mb-6 text-2xl font-semibold text-text-primary">
    {{ esEdicion() ? ('[feature].form.editar' | transloco) : ('[feature].form.crear' | transloco) }}
  </h1>

  @if (error()) {
    <div role="alert" class="mb-4 rounded-lg bg-error-subtle p-4 text-sm text-error">
      {{ error() }}
    </div>
  }

  <form [formGroup]="form" (ngSubmit)="guardar()" class="space-y-4">
    <fieldset>
      <legend class="text-lg font-medium text-text-primary">{{ '[feature].form.datos' | transloco }}</legend>

      <div class="mt-4">
        <label for="campo" class="block text-sm font-medium text-text-primary">
          {{ '[feature].form.campo' | transloco }}
        </label>
        <input
          id="campo"
          type="text"
          formControlName="campo"
          class="mt-1 block w-full rounded-lg border border-border-default bg-bg-base px-3 py-2 text-sm text-text-primary placeholder:text-text-disabled focus:border-primary focus:ring-primary"
        />
      </div>
    </fieldset>

    <div class="flex items-center gap-3 pt-4">
      <button type="submit" class="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-text shadow-sm transition-colors hover:bg-primary-hover">
        {{ 'common.guardar' | transloco }}
      </button>
      <a routerLink="/[feature]" class="inline-flex items-center gap-2 rounded-lg border border-border-default bg-bg-base px-4 py-2 text-sm font-semibold text-text-primary transition-colors hover:bg-bg-subtle">
        {{ 'common.cancelar' | transloco }}
      </a>
    </div>
  </form>
</section>
```

---

## 8. Patrón de Componente Dumb (Presentación)

```typescript
// shared/ui/[feature]-card.ts  o  pages/[feature]/components/[feature]-card.ts
@Component({
  selector: 'app-[feature]-card',
  standalone: true,
  imports: [RouterLink, TranslocoModule, AppBadgeComponent],
  templateUrl: './[feature]-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class [Feature]CardComponent {
  // Solo inputs y outputs — sin inyección de servicios
  item = input.required<[Feature]>();
  onEliminar = output<number>();
  onToggle = output<number>();
}
```

**Reglas de dumb components:**
- Solo `input()` y `output()` — nunca `inject()` de servicios de negocio
- Sin lógica de negocio — solo transformaciones de presentación con `computed()`
- Si necesita traducciones: importar `TranslocoModule` y usar pipe, nunca inyectar `TranslocoService`
- Reutilizable: si se usa en 2+ contextos → `shared/ui/`, si es específico → carpeta de la feature

---

## 9. Patrón de Guard Funcional

### Síncrono — cuando los datos ya están disponibles

```typescript
// auth/permission.guard.ts
export function permissionGuard(check: (auth: AuthService) => boolean): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return check(auth) ? true : router.createUrlTree(['/dashboard']);
  };
}

// Uso en rutas:
canActivate: [permissionGuard(a => a.canManage[Feature]())]
```

### Asíncrono — cuando el signal arranca en null

```typescript
// auth/user-registered.guard.ts
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';

export const userRegisteredGuard: CanActivateFn = () => {
  const usuariosService = inject(UsuariosService);
  const authService = inject(AuthService);
  const router = inject(Router);

  return toObservable(authService.userData).pipe(
    filter(userData => userData !== null),
    take(1),
    map(() => {
      const email = authService.email() ?? '';
      return usuariosService.existePorEmail(email)
        ? true
        : router.createUrlTree(['/acceso-denegado']);
    }),
  );
};
```

**Cuándo usar cada uno:**
- **Síncrono:** el signal ya fue populado (roles disponibles, estado local inicializado)
- **Asíncrono:** el signal empieza en `null` y se popula vía `subscribe()` (userData de OIDC, datos de API)

---

## 10. Patrón Adapter de Plataforma

### Paso 1: Token + Interfaz

```typescript
// shared/platform/[nombre].token.ts
import { InjectionToken } from '@angular/core';

export interface [Nombre]Adapter {
  // métodos de la abstracción
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
}

export const [NOMBRE]_ADAPTER = new InjectionToken<[Nombre]Adapter>('[Nombre]Adapter');
```

### Paso 2: Implementación Browser

```typescript
// shared/platform/[nombre].browser.ts
import { Injectable } from '@angular/core';
import { [Nombre]Adapter } from './[nombre].token';

@Injectable()
export class Browser[Nombre]Adapter implements [Nombre]Adapter {
  get(key: string): string | null {
    return sessionStorage.getItem(key);
  }
  set(key: string, value: string): void {
    sessionStorage.setItem(key, value);
  }
  remove(key: string): void {
    sessionStorage.removeItem(key);
  }
}
```

### Paso 3: Registro en app.config.ts

```typescript
{ provide: [NOMBRE]_ADAPTER, useClass: Browser[Nombre]Adapter },
```

### Paso 4: Consumo en servicios

```typescript
@Injectable({ providedIn: 'root' })
export class MiServicio {
  private readonly adapter = inject([NOMBRE]_ADAPTER);

  leerPreferencia(): string | null {
    return this.adapter.get('mi-preferencia');
  }
}
```

**Regla:** nunca acceder a `window`, `document`, `sessionStorage`, `localStorage` directamente en servicios o componentes — siempre a través del adapter correspondiente.

---

## 11. Patrón de Traducciones para Feature Nueva

### Estructura de claves en `es.json` / `en.json`

```json
{
  "[feature]": {
    "lista": {
      "titulo": "Listado de [features]",
      "descripcion": "Gestionar [features] del sistema",
      "vacio": "No se encontraron [features]"
    },
    "detalle": {
      "titulo": "Detalle de [feature]"
    },
    "form": {
      "crear": "Nuevo [feature]",
      "editar": "Editar [feature]",
      "datos": "Datos del [feature]",
      "campo": "Nombre del campo"
    }
  }
}
```

**Reglas:**
- Máximo 3 niveles: `modulo.seccion.clave`
- JSON plano — nunca arrays ni objetos anidados como valores
- Siempre agregar ambos idiomas (es.json y en.json)
- Reutilizar claves de `common.*` para acciones genéricas (guardar, cancelar, eliminar, cargando)
