# DESIGN-TOKENS.md — Tokens de Diseño y Paleta de Colores

> Referencia completa de CSS variables, tokens semánticos y mapeo a clases Tailwind.
> Claude debe usar exclusivamente estos tokens — nunca colores directos de Tailwind en componentes.

---

## Regla Principal

```html
<!-- ✅ CORRECTO — tokens semánticos -->
<div class="bg-bg-base border border-border-default text-text-primary">
  <button class="bg-primary text-primary-text hover:bg-primary-hover">Acción</button>
</div>

<!-- ❌ INCORRECTO — colores directos de Tailwind -->
<div class="bg-white border border-slate-200 text-slate-900">
  <button class="bg-sky-600 text-white hover:bg-sky-700">Acción</button>
</div>
```

---

## Tokens Invariantes (no cambian con el tema)

Definidos en `src/styles/themes/_tokens.css`:

| Token CSS | Valor Tailwind | Uso |
|-----------|---------------|-----|
| `--spacing-xs` | `spacing.1` (0.25rem) | Gaps mínimos, padding de badges |
| `--spacing-sm` | `spacing.2` (0.5rem) | Gaps entre iconos y texto |
| `--spacing-md` | `spacing.4` (1rem) | Padding interno estándar |
| `--spacing-lg` | `spacing.6` (1.5rem) | Separación entre secciones |
| `--spacing-xl` | `spacing.8` (2rem) | Separación entre bloques principales |
| `--radius-sm` | `borderRadius.md` | Inputs, badges |
| `--radius-md` | `borderRadius.lg` | Botones, cards pequeñas |
| `--radius-lg` | `borderRadius.xl` | Cards principales, modales |
| `--font-size-sm` | `fontSize.sm` (0.875rem) | Labels, captions, badges |
| `--font-size-base` | `fontSize.base` (1rem) | Body text |
| `--font-size-lg` | `fontSize.lg` (1.125rem) | Subtítulos de sección |
| `--font-size-xl` | `fontSize.xl` (1.25rem) | Títulos de sección |
| `--font-size-2xl` | `fontSize.2xl` (1.5rem) | Título de página (h1) |
| `--shadow-sm` | `boxShadow.sm` | Cards sutiles, dropdowns |
| `--shadow-md` | `boxShadow.md` | Cards elevadas, modales |
| `--shadow-lg` | `boxShadow.lg` | Popovers, elementos flotantes |
| `--transition-base` | `150ms ease-in-out` | Transiciones por defecto |

---

## Tokens de Color — Modo Claro y Oscuro

### Superficie (backgrounds)

| Token CSS | Clase Tailwind | Light | Dark | Uso |
|-----------|---------------|-------|------|-----|
| `--color-bg-base` | `bg-bg-base` | `white` | `slate.950` | Fondo principal de la app, cards |
| `--color-bg-subtle` | `bg-bg-subtle` | `slate.50` | `slate.900` | Fondo secundario, hover de filas |
| `--color-bg-muted` | `bg-bg-muted` | `slate.100` | `slate.800` | Badges neutral, fondos desactivados |
| `--color-bg-emphasis` | `bg-bg-emphasis` | `slate.200` | `slate.700` | Elementos con énfasis |

### Texto

| Token CSS | Clase Tailwind | Light | Dark | Uso |
|-----------|---------------|-------|------|-----|
| `--color-text-primary` | `text-text-primary` | `slate.900` | `slate.50` | Texto principal, títulos |
| `--color-text-secondary` | `text-text-secondary` | `slate.600` | `slate.400` | Texto secundario, descripciones |
| `--color-text-disabled` | `text-text-disabled` | `slate.400` | `slate.600` | Texto desactivado, placeholders |
| `--color-text-inverse` | `text-text-inverse` | `white` | `slate.900` | Texto sobre fondos oscuros |

### Bordes

| Token CSS | Clase Tailwind | Light | Dark | Uso |
|-----------|---------------|-------|------|-----|
| `--color-border-default` | `border-border-default` | `slate.200` | `slate.700` | Bordes estándar (cards, inputs, tablas) |
| `--color-border-strong` | `border-border-strong` | `slate.400` | `slate.500` | Bordes enfatizados, separadores |

### Brand / Primario

| Token CSS | Clase Tailwind | Light | Dark | Uso |
|-----------|---------------|-------|------|-----|
| `--color-primary` | `bg-primary` / `text-primary` | `sky.600` | `sky.400` | Botones primarios, links, iconos activos |
| `--color-primary-hover` | `hover:bg-primary-hover` | `sky.700` | `sky.300` | Hover de elementos primarios |
| `--color-primary-subtle` | `bg-primary-subtle` | `sky.50` | `sky.950` | Fondo de badges/alerts primarios |
| `--color-primary-text` | `text-primary-text` | `white` | `slate.900` | Texto sobre fondo primario |

### Semánticos — Estado

| Token CSS | Clase Tailwind | Light | Dark | Uso |
|-----------|---------------|-------|------|-----|
| `--color-success` | `text-success` / `bg-success` | `emerald.600` | `emerald.400` | Éxito, activo, completado |
| `--color-success-subtle` | `bg-success-subtle` | `emerald.50` | `emerald.950` | Fondo de badges/alerts de éxito |
| `--color-warning` | `text-warning` / `bg-warning` | `amber.500` | `amber.400` | Advertencias, pendiente |
| `--color-warning-subtle` | `bg-warning-subtle` | `amber.50` | `amber.950` | Fondo de badges/alerts de advertencia |
| `--color-error` | `text-error` / `bg-error` | `red.600` | `red.400` | Errores, eliminación, peligro |
| `--color-error-subtle` | `bg-error-subtle` | `red.50` | `red.950` | Fondo de badges/alerts de error |
| `--color-info` | `text-info` / `bg-info` | `blue.600` | `blue.400` | Información, estados neutros |
| `--color-info-subtle` | `bg-info-subtle` | `blue.50` | `blue.950` | Fondo de badges/alerts informativos |

---

## Mapeo en tailwind.config.js

Todas las clases Tailwind anteriores funcionan porque están mapeadas en `tailwind.config.js` así:

```javascript
colors: {
  "bg-base":        "var(--color-bg-base)",
  "bg-subtle":      "var(--color-bg-subtle)",
  "bg-muted":       "var(--color-bg-muted)",
  "bg-emphasis":    "var(--color-bg-emphasis)",
  "text-primary":   "var(--color-text-primary)",
  "text-secondary": "var(--color-text-secondary)",
  "text-disabled":  "var(--color-text-disabled)",
  "text-inverse":   "var(--color-text-inverse)",
  "border-default": "var(--color-border-default)",
  "border-strong":  "var(--color-border-strong)",
  primary:          "var(--color-primary)",
  "primary-hover":  "var(--color-primary-hover)",
  "primary-subtle": "var(--color-primary-subtle)",
  "primary-text":   "var(--color-primary-text)",
  success:          "var(--color-success)",
  "success-subtle": "var(--color-success-subtle)",
  warning:          "var(--color-warning)",
  "warning-subtle": "var(--color-warning-subtle)",
  error:            "var(--color-error)",
  "error-subtle":   "var(--color-error-subtle)",
  info:             "var(--color-info)",
  "info-subtle":    "var(--color-info-subtle)",
}
```

---

## Combinaciones Correctas por Contexto

### Página / Layout
```html
<main class="bg-bg-subtle min-h-screen">
  <div class="mx-auto max-w-7xl px-4 py-6">
    <!-- contenido -->
  </div>
</main>
```

### Card estándar
```html
<div class="rounded-xl border border-border-default bg-bg-base p-6 shadow-sm">
  <h2 class="text-lg font-semibold text-text-primary">Título</h2>
  <p class="mt-1 text-sm text-text-secondary">Descripción</p>
</div>
```

### Tabla de datos
```html
<table class="w-full text-sm text-text-primary">
  <thead class="border-b border-border-default bg-bg-subtle">
    <tr>
      <th class="px-4 py-3 text-left text-xs font-medium text-text-secondary uppercase">Columna</th>
    </tr>
  </thead>
  <tbody class="divide-y divide-border-default">
    <tr class="hover:bg-bg-subtle transition-colors">
      <td class="px-4 py-3">Valor</td>
    </tr>
  </tbody>
</table>
```

### Alerta / Mensaje de estado
```html
<!-- Éxito -->
<div class="rounded-lg bg-success-subtle p-4 text-sm text-success">Operación completada.</div>

<!-- Error -->
<div class="rounded-lg bg-error-subtle p-4 text-sm text-error">Ha ocurrido un error.</div>

<!-- Advertencia -->
<div class="rounded-lg bg-warning-subtle p-4 text-sm text-warning">Atención: datos sin guardar.</div>

<!-- Información -->
<div class="rounded-lg bg-info-subtle p-4 text-sm text-info">Nuevo: funcionalidad disponible.</div>
```

### Badge de estado
```html
<span class="inline-flex items-center gap-1 rounded-full bg-success-subtle px-2.5 py-0.5 text-xs font-medium text-success">Activo</span>
<span class="inline-flex items-center gap-1 rounded-full bg-error-subtle px-2.5 py-0.5 text-xs font-medium text-error">Inactivo</span>
<span class="inline-flex items-center gap-1 rounded-full bg-warning-subtle px-2.5 py-0.5 text-xs font-medium text-warning">Pendiente</span>
```

### Input con error
```html
<!-- Normal -->
<input class="block w-full rounded-lg border border-border-default bg-bg-base px-3 py-2 text-sm text-text-primary placeholder:text-text-disabled focus:border-primary focus:ring-primary" />

<!-- Con error -->
<input class="block w-full rounded-lg border border-error bg-bg-base px-3 py-2 text-sm text-text-primary placeholder:text-text-disabled focus:border-error focus:ring-error" />
<p class="mt-1 text-xs text-error">Este campo es obligatorio.</p>
```

---

## Clases Reutilizables en components.css

Patrones extraídos con `@apply` para evitar repetición:

| Clase CSS | Uso |
|-----------|-----|
| `.btn-primary` | Botón primario completo con hover, focus, disabled |
| `.card` | Card con borde, fondo, padding y sombra |
| `.form-input` | Input de formulario con estados focus y placeholder |

Usar estas clases solo cuando el componente NO usa el objeto `THEME` (ej. en HTML estático). Los componentes Angular deben preferir el objeto `THEME` vía `[ngClass]`.

---

## Dark Mode

El dark mode se controla por atributo `data-theme` en el `<html>`, no por clase.

```javascript
// tailwind.config.js
darkMode: ["selector", '[data-theme="dark"]'],
```

Las CSS variables se actualizan automáticamente al cambiar `data-theme`. No es necesario usar `dark:` variants de Tailwind — los tokens semánticos ya resuelven ambos modos.

**Excepción:** la variante `violet` del badge usa `dark:` directamente porque no tiene CSS variable propia:
```
bg-violet-100 text-violet-700 dark:bg-violet-900/30 dark:text-violet-400
```

---

## Estructura de Archivos de Estilos

```
src/styles/
├── themes/
│   ├── _tokens.css        → Variables invariantes (spacing, radius, shadows)
│   ├── _theme-light.css   → Paleta modo claro
│   └── _theme-dark.css    → Paleta modo oscuro
├── components.css         → Clases reutilizables con @apply
└── index.css              → Punto de entrada (@tailwind + imports)
```
