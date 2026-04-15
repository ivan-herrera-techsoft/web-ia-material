# STYLES-GUIDE.md — Guía Visual de Estilos y Layouts

> Define los patrones visuales recurrentes para mantener consistencia en la UI.
> Claude debe seguir estos layouts estándar al crear páginas y componentes.

---

## Tipografía por Contexto

| Contexto | Clase Tailwind | Ejemplo |
|----------|---------------|---------|
| Título de página (h1) | `text-2xl font-semibold text-text-primary` | "Listado de Clientes" |
| Subtítulo de página | `text-sm text-text-secondary` | "Gestionar clientes del sistema" |
| Título de sección (h2) | `text-lg font-semibold text-text-primary` | "Datos generales" |
| Título de card (h3) | `text-base font-medium text-text-primary` | "Información de contacto" |
| Body text | `text-sm text-text-primary` | Contenido general |
| Texto secundario | `text-sm text-text-secondary` | Descripciones, ayuda |
| Label de formulario | `text-sm font-medium text-text-primary` | "Nombre del cliente" |
| Caption / helper | `text-xs text-text-secondary` | "Mínimo 3 caracteres" |
| Error de validación | `text-xs text-error` | "Este campo es obligatorio" |
| Header de tabla | `text-xs font-medium text-text-secondary uppercase` | "REGIÓN" |

**Regla:** un solo `<h1>` por página. Jerarquía h1 → h2 → h3 sin saltar niveles.

---

## Spacing System

### Entre secciones de una página

| Situación | Clase | Valor |
|-----------|-------|-------|
| Entre header de página y contenido | `mb-6` | 1.5rem |
| Entre secciones principales | `space-y-6` o `mt-6` | 1.5rem |
| Entre cards en un grid | `gap-4` o `gap-6` | 1rem / 1.5rem |
| Entre campos de formulario | `space-y-4` | 1rem |
| Entre label y input | `mt-1` | 0.25rem |
| Entre input y mensaje de error | `mt-1` | 0.25rem |
| Entre botones de acción | `gap-3` | 0.75rem |
| Padding de card sm | `p-4` | 1rem |
| Padding de card md | `p-6` | 1.5rem |
| Padding de card lg | `p-8` | 2rem |
| Padding de celda de tabla | `px-4 py-3` | 1rem / 0.75rem |

---

## Layouts de Página

### Página Lista (el más común)

```
┌──────────────────────────────────────────────────┐
│ [Header: h1 + descripción]          [Btn Crear]  │
├──────────────────────────────────────────────────┤
│ [Input de búsqueda / filtros]                    │
├──────────────────────────────────────────────────┤
│ [Tabla o grid de cards]                          │
│   Fila 1                                         │
│   Fila 2                                         │
│   Fila 3                                         │
├──────────────────────────────────────────────────┤
│ [Paginación (si aplica)]                         │
└──────────────────────────────────────────────────┘
```

```html
<section>
  <!-- Header -->
  <header class="mb-6 flex items-center justify-between">
    <div>
      <h1 class="text-2xl font-semibold text-text-primary">Título</h1>
      <p class="mt-1 text-sm text-text-secondary">Descripción</p>
    </div>
    <a routerLink="nuevo" class="...btn-primary...">Crear nuevo</a>
  </header>

  <!-- Filtros -->
  <div class="mb-4 flex gap-3">
    <input type="text" class="...form-input..." />
    <!-- selectores de filtro adicionales -->
  </div>

  <!-- Contenido -->
  <div class="rounded-xl border border-border-default bg-bg-base shadow-sm">
    <table class="w-full text-sm">
      <!-- ... -->
    </table>
  </div>
</section>
```

---

### Página Detalle

```
┌──────────────────────────────────────────────────┐
│ [← Volver]                    [Btn Editar] [Btn] │
├──────────────────────────────────────────────────┤
│ [Card: Información principal]                    │
│   Campo 1: Valor                                 │
│   Campo 2: Valor                                 │
├──────────────────────────────────────────────────┤
│ [Card: Sección secundaria]                       │
│   ...                                            │
└──────────────────────────────────────────────────┘
```

```html
<section>
  <!-- Header con navegación -->
  <header class="mb-6 flex items-center justify-between">
    <a routerLink=".." class="inline-flex items-center gap-1 text-sm text-text-secondary hover:text-text-primary">
      ← Volver
    </a>
    <div class="flex gap-3">
      <a [routerLink]="['editar']" class="...btn-secondary...">Editar</a>
      <button type="button" class="...btn-danger..." (click)="eliminar()">Eliminar</button>
    </div>
  </header>

  <!-- Contenido en cards -->
  <div class="space-y-6">
    <div class="rounded-xl border border-border-default bg-bg-base p-6 shadow-sm">
      <h2 class="mb-4 text-lg font-semibold text-text-primary">Información general</h2>
      <dl class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <dt class="text-xs font-medium text-text-secondary uppercase">Campo</dt>
          <dd class="mt-1 text-sm text-text-primary">Valor</dd>
        </div>
      </dl>
    </div>
  </div>
</section>
```

---

### Página Formulario

```
┌──────────────────────────────────────────────────┐
│ [h1: Crear/Editar Feature]                       │
├──────────────────────────────────────────────────┤
│ [Alert de error (si hay)]                        │
├──────────────────────────────────────────────────┤
│ <form>                                           │
│   <fieldset> Datos generales                     │
│     Label + Input                                │
│     Label + Input                                │
│   </fieldset>                                    │
│                                                  │
│   <fieldset> Otra sección                        │
│     Label + Select                               │
│     Label + Textarea                             │
│   </fieldset>                                    │
│                                                  │
│   [Btn Guardar] [Btn Cancelar]                   │
│ </form>                                          │
└──────────────────────────────────────────────────┘
```

```html
<section>
  <h1 class="mb-6 text-2xl font-semibold text-text-primary">Título</h1>

  @if (error()) {
    <div role="alert" class="mb-4 rounded-lg bg-error-subtle p-4 text-sm text-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="guardar()" class="space-y-6">
    <fieldset class="space-y-4">
      <legend class="text-lg font-medium text-text-primary">Datos generales</legend>
      <div>
        <label for="nombre" class="block text-sm font-medium text-text-primary">Nombre</label>
        <input id="nombre" type="text" formControlName="nombre"
               class="mt-1 block w-full rounded-lg border border-border-default bg-bg-base px-3 py-2 text-sm text-text-primary placeholder:text-text-disabled focus:border-primary focus:ring-primary" />
      </div>
    </fieldset>

    <div class="flex items-center gap-3 border-t border-border-default pt-6">
      <button type="submit" class="...btn-primary...">Guardar</button>
      <a routerLink=".." class="...btn-secondary...">Cancelar</a>
    </div>
  </form>
</section>
```

---

## Responsive Patterns

### Breakpoints (mobile-first)

| Breakpoint | Prefijo | Uso típico |
|------------|---------|-----------|
| < 640px | (base) | Stack vertical, una columna |
| ≥ 640px | `sm:` | Dos columnas en formularios |
| ≥ 768px | `md:` | Sidebar visible, grid de 2-3 columnas |
| ≥ 1024px | `lg:` | Grid de 3-4 columnas, layouts amplios |
| ≥ 1280px | `xl:` | Contenedores max-width |

### Patrones de colapso

```html
<!-- Grid que colapsa a stack -->
<div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
  <!-- cards -->
</div>

<!-- Formulario en columnas que colapsa -->
<div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
  <div><!-- campo 1 --></div>
  <div><!-- campo 2 --></div>
</div>

<!-- Header que colapsa botones debajo -->
<header class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
  <div><!-- título --></div>
  <div class="flex gap-3"><!-- botones --></div>
</header>

<!-- Tabla responsiva -->
<div class="overflow-x-auto rounded-xl border border-border-default">
  <table class="w-full min-w-[600px] text-sm">
    <!-- ... -->
  </table>
</div>
```

**Regla:** nunca media queries manuales — usar breakpoints de Tailwind.

---

## Tabla de Datos Estándar

```html
<div class="overflow-x-auto rounded-xl border border-border-default bg-bg-base shadow-sm">
  <table class="w-full text-sm text-text-primary">
    <caption class="sr-only">Descripción de la tabla para lectores de pantalla</caption>
    <thead class="border-b border-border-default bg-bg-subtle">
      <tr>
        <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase">
          Nombre
        </th>
        <th scope="col" class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase">
          Estado
        </th>
        <th scope="col" class="px-4 py-3 text-right text-xs font-medium text-text-secondary uppercase">
          Acciones
        </th>
      </tr>
    </thead>
    <tbody class="divide-y divide-border-default">
      @for (item of items(); track item.id) {
        <tr class="transition-colors hover:bg-bg-subtle">
          <td class="px-4 py-3 font-medium">{{ item.nombre }}</td>
          <td class="px-4 py-3">
            <app-badge [variant]="item.activo ? 'success' : 'danger'">
              {{ item.activo ? ('common.activo' | transloco) : ('common.inactivo' | transloco) }}
            </app-badge>
          </td>
          <td class="px-4 py-3 text-right">
            <a [routerLink]="[item.id]"
               class="text-sm font-medium text-primary hover:text-primary-hover transition-colors">
              {{ 'common.ver' | transloco }}
            </a>
          </td>
        </tr>
      } @empty {
        <tr>
          <td colspan="3" class="px-4 py-8 text-center text-sm text-text-secondary">
            {{ 'common.sin_resultados' | transloco }}
          </td>
        </tr>
      }
    </tbody>
  </table>
</div>
```

---

## Alertas y Feedback

### Mensaje de estado en línea

```html
<!-- Éxito -->
<div role="alert" class="rounded-lg bg-success-subtle p-4 text-sm text-success">
  {{ 'mensaje.exito' | transloco }}
</div>

<!-- Error -->
<div role="alert" class="rounded-lg bg-error-subtle p-4 text-sm text-error">
  {{ error() }}
</div>

<!-- Advertencia -->
<div role="alert" class="rounded-lg bg-warning-subtle p-4 text-sm text-warning">
  {{ 'mensaje.advertencia' | transloco }}
</div>

<!-- Sin conexión (PWA) -->
<div role="alert" class="rounded-lg bg-warning-subtle p-4 text-sm text-warning">
  {{ 'common.sin_conexion' | transloco }}
</div>
```

### Estado vacío

```html
<div class="flex flex-col items-center justify-center py-12">
  <p class="text-sm text-text-secondary">{{ '[feature].lista.vacio' | transloco }}</p>
  <a routerLink="nuevo" class="mt-3 text-sm font-medium text-primary hover:text-primary-hover">
    {{ 'common.crear_primero' | transloco }}
  </a>
</div>
```

### Estado de carga

```html
<div class="flex items-center justify-center py-12">
  <p class="text-sm text-text-secondary">{{ 'common.cargando' | transloco }}</p>
</div>
```

---

## Sidebar y Header — Estructura Global

```
┌──────┬─────────────────────────────────────────────┐
│      │ [Header: usuario, notificaciones, logout]    │
│  S   ├─────────────────────────────────────────────┤
│  I   │                                             │
│  D   │  <main class="bg-bg-subtle min-h-screen">  │
│  E   │    <div class="mx-auto max-w-7xl px-4      │
│  B   │         py-6 sm:px-6 lg:px-8">             │
│  A   │                                             │
│  R   │      <!-- RouterOutlet aquí -->             │
│      │                                             │
│      │    </div>                                   │
│      │  </main>                                    │
└──────┴─────────────────────────────────────────────┘
```

- Sidebar en `sidebar/` — navegación lateral colapsable
- Header en `header/` — barra superior
- Contenido principal con `bg-bg-subtle` y contenedor centrado con `max-w-7xl`
- Padding lateral responsivo: `px-4 sm:px-6 lg:px-8`
