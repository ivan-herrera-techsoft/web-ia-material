# UI-COMPONENTS.md — Catálogo de Componentes UI y Design System

> Fuente única de verdad para componentes reutilizables y variantes del sistema de diseño.
> Antes de crear un componente UI, verificar si ya existe en `shared/ui/`.
> Los componentes consumen variantes del objeto `THEME` — nunca clases de color directas.

---

## Regla Principal

1. **Verificar antes de crear:** si el componente ya existe en `shared/ui/`, usarlo
2. **Consumir `THEME`:** usar `[ngClass]="THEME.button[variant()]"` — nunca clases de color hardcodeadas
3. **Componentes dumb:** solo `input()` / `output()`, sin inyección de servicios de negocio
4. **Siempre standalone** con `ChangeDetectionStrategy.OnPush`

---

## Objeto THEME — Definición Completa

Ubicación: `src/app/theme/theme.ts`

```typescript
export const THEME = {

  button: {
    primary:
      'inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-text shadow-sm transition-colors hover:bg-primary-hover focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 disabled:opacity-50',
    secondary:
      'inline-flex items-center gap-2 rounded-lg border border-border-default bg-bg-base px-4 py-2 text-sm font-semibold text-text-primary transition-colors hover:bg-bg-subtle focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 disabled:opacity-50',
    outline:
      'inline-flex items-center gap-2 rounded-lg border border-primary bg-transparent px-4 py-2 text-sm font-semibold text-primary transition-colors hover:bg-primary-subtle focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 disabled:opacity-50',
    ghost:
      'inline-flex items-center gap-2 rounded-lg bg-transparent px-4 py-2 text-sm font-semibold text-text-secondary transition-colors hover:bg-bg-subtle hover:text-text-primary focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-2 disabled:opacity-50',
    danger:
      'inline-flex items-center gap-2 rounded-lg bg-error px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-error focus:ring-offset-2 disabled:opacity-50',
  },

  badge: {
    success: 'inline-flex items-center gap-1 rounded-full bg-success-subtle px-2.5 py-0.5 text-xs font-medium text-success',
    neutral: 'inline-flex items-center gap-1 rounded-full bg-bg-muted       px-2.5 py-0.5 text-xs font-medium text-text-secondary',
    primary: 'inline-flex items-center gap-1 rounded-full bg-primary-subtle  px-2.5 py-0.5 text-xs font-medium text-primary',
    warning: 'inline-flex items-center gap-1 rounded-full bg-warning-subtle  px-2.5 py-0.5 text-xs font-medium text-warning',
    danger:  'inline-flex items-center gap-1 rounded-full bg-error-subtle    px-2.5 py-0.5 text-xs font-medium text-error',
    blue:    'inline-flex items-center gap-1 rounded-full bg-info-subtle     px-2.5 py-0.5 text-xs font-medium text-info',
    violet:  'inline-flex items-center gap-1 rounded-full bg-violet-100 px-2.5 py-0.5 text-xs font-medium text-violet-700 dark:bg-violet-900/30 dark:text-violet-400',
  },

  card: {
    base: 'rounded-xl border border-border-default bg-bg-base shadow-sm',
    padding: {
      sm: 'p-4',
      md: 'p-6',
      lg: 'p-8',
    },
  },

  input: {
    base:    'block w-full rounded-lg border px-3 py-2 text-sm transition-colors focus:outline-none focus:ring-2 focus:ring-offset-0',
    default: 'border-border-default bg-bg-base text-text-primary placeholder:text-text-disabled focus:border-primary focus:ring-primary',
    error:   'border-error bg-bg-base text-text-primary placeholder:text-text-disabled focus:border-error focus:ring-error',
  },

} as const;

export type ButtonVariant = keyof typeof THEME.button;
export type BadgeVariant  = keyof typeof THEME.badge;
export type CardPadding   = keyof typeof THEME.card.padding;
export type InputVariant  = keyof typeof THEME.input;
```

---

## Inventario de Componentes — `shared/ui/`

### `<app-button>`

Botón reutilizable con variantes del THEME.

| Input | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `variant` | `ButtonVariant` | `'primary'` | Estilo visual |
| `type` | `'button' \| 'submit'` | `'button'` | Tipo HTML |
| `disabled` | `boolean` | `false` | Estado deshabilitado |

```html
<!-- Uso en templates -->
<app-button variant="primary" (click)="guardar()">Guardar</app-button>
<app-button variant="secondary" (click)="cancelar()">Cancelar</app-button>
<app-button variant="danger" (click)="eliminar()">Eliminar</app-button>
<app-button variant="ghost" (click)="cerrar()">Cerrar</app-button>
<app-button variant="outline">Exportar</app-button>
```

```typescript
// Implementación de referencia
@Component({
  selector: 'app-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [ngClass]="THEME.button[variant()]"
    >
      <ng-content />
    </button>
  `,
  imports: [NgClass],
})
export class AppButtonComponent {
  readonly THEME = THEME;
  variant = input<ButtonVariant>('primary');
  type = input<'button' | 'submit'>('button');
  disabled = input(false, { transform: booleanAttribute });
}
```

---

### `<app-badge>`

Badge / chip para estados y etiquetas.

| Input | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `variant` | `BadgeVariant` | `'neutral'` | Estilo visual |

```html
<app-badge variant="success">Activo</app-badge>
<app-badge variant="danger">Inactivo</app-badge>
<app-badge variant="warning">Pendiente</app-badge>
<app-badge variant="primary">Nuevo</app-badge>
<app-badge variant="neutral">Borrador</app-badge>
<app-badge variant="blue">Info</app-badge>
<app-badge variant="violet">Premium</app-badge>
```

```typescript
@Component({
  selector: 'app-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [ngClass]="THEME.badge[variant()]"><ng-content /></span>`,
  imports: [NgClass],
})
export class AppBadgeComponent {
  readonly THEME = THEME;
  variant = input<BadgeVariant>('neutral');
}
```

---

### `<app-card>`

Contenedor con borde, fondo y sombra.

| Input | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `padding` | `CardPadding` | `'md'` | Tamaño de padding interno |

```html
<app-card>Contenido con padding md (default)</app-card>
<app-card padding="sm">Contenido compacto</app-card>
<app-card padding="lg">Contenido espacioso</app-card>
```

```typescript
@Component({
  selector: 'app-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [ngClass]="[THEME.card.base, THEME.card.padding[padding()]]">
      <ng-content />
    </div>
  `,
  imports: [NgClass],
})
export class AppCardComponent {
  readonly THEME = THEME;
  padding = input<CardPadding>('md');
}
```

---

### `<app-select>`

Select nativo estilizado con tokens del design system.

| Input | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `options` | `{ value: string; label: string }[]` | `[]` | Opciones del select |
| `placeholder` | `string` | `''` | Placeholder |

| Output | Tipo | Descripción |
|--------|------|-------------|
| `valueChange` | `string` | Valor seleccionado |

```html
<app-select
  [options]="regiones()"
  placeholder="Seleccionar región"
  (valueChange)="onRegionChange($event)"
/>
```

---

### `<app-tabs>`

Navegación por pestañas.

| Input | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `tabs` | `{ id: string; label: string }[]` | `[]` | Pestañas disponibles |
| `activeTab` | `string` | `''` | Pestaña activa |

| Output | Tipo | Descripción |
|--------|------|-------------|
| `tabChange` | `string` | ID de la pestaña seleccionada |

```html
<app-tabs
  [tabs]="[{ id: 'general', label: 'General' }, { id: 'permisos', label: 'Permisos' }]"
  [activeTab]="tabActivo()"
  (tabChange)="tabActivo.set($event)"
/>
```

---

### `<app-theme-toggle>`

Toggle para cambiar entre modo claro y oscuro.

Sin inputs ni outputs — inyecta `ThemeService` internamente.

```html
<app-theme-toggle />
```

---

## Cómo Agregar un Nuevo Componente UI

1. Crear archivo en `shared/ui/[nombre-componente].ts`
2. Componente standalone con `ChangeDetectionStrategy.OnPush`
3. Usar `input()` / `output()` signals — nunca decoradores
4. Si necesita estilos del THEME, agregar la variante al objeto `THEME` en `theme/theme.ts`
5. Exportar el tipo de variante: `export type NuevoVariant = keyof typeof THEME.nuevo;`
6. Documentar en este archivo con: selector, inputs, outputs, ejemplo de uso

**Regla:** un componente nuevo en `shared/ui/` debe ser genérico y reutilizable en al menos 2 contextos. Si es específico de una feature, va en la carpeta de la feature, no en `shared/ui/`.
