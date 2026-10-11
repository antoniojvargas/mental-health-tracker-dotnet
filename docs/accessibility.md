# Accesibilidad

Auditoría y correcciones de accesibilidad del frontend. Todo lo aquí descrito se
corrigió en la rama `chore/accessibility-and-polish`.

## Método de auditoría

- **Lighthouse** (categoría *Accessibility*) sobre `/login` sin sesión,
  corriendo el frontend en dev.
- **axe-core 4.10.2** inyectado vía Playwright sobre `/login` sin sesión,
  `/dashboard` autenticado y los cuatro pasos del modal de registro diario
  (incluyendo el estado con severidad de síntoma activa), con las reglas
  `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa` y `best-practice`.
- **Ratios de contraste WCAG 2.x calculados a mano** para los pares que la
  herramienta no puede valorar en estado no renderizado (hover, texto sobre
  colores de serie).

## Hallazgos y correcciones

### Contraste de texto (WCAG 1.4.3, AA: 4.5:1)

| Elemento | Antes | Ratio | Después | Ratio |
| --- | --- | --- | --- | --- |
| Botón `primary` (`Button.vue`) | `bg-clearsky-600` / texto `paper-50` | 4.39 | `bg-clearsky-700`, hover `clearsky-800` (nuevo en la paleta) | 6.36 / 8.10 |
| Opción inactiva de `RangeToggle` | `ink-400` sobre `paper-200` | 3.91 | `ink-500` | 6.35 |
| Chip de métrica activo (`MetricSelector`) | texto `paper-50` sobre el color de la serie | 3.12 / 3.06 | `bg-ink-700` + punto con el color real | >12 |
| Nota de privacidad del login | `ink-300` sobre `paper-50` | 2.75 | `ink-400` | 4.84 |

Los colores de las series (`METRICS`) están pensados para trazos de gráfica: su
luminosidad media no da 4.5:1 con texto ni claro ni oscuro (p. ej. ánimo: 3.58
con texto `ink-700`). Por eso el chip activo se rellena con `ink-700` y la
correspondencia con la serie la conserva el punto de color, que no es texto.

### Etiquetas de formulario (WCAG 1.3.1, 4.1.2)

- Verificadas: `role="radiogroup"` de ánimo, *sliders* con `<label for>`,
  *selects* y *textarea* con `<label>`, cajas con `<label>` envolvente o `for`.
  Sin hallazgos en runtime.
- Se añadió `aria-required="true"` al grupo de ánimo, que es el único campo
  obligatorio (el botón `Next` no avanza sin él).
- Se reemplazó el color inexistente `text-ink-900` (no definido en la paleta,
  Tailwind lo ignoraba) por `text-ink-700` en etiquetas, leyendas y encabezados:
  antes el color real era el heredado del navegador, no el intencionado.

### Orden de foco y teclado (WCAG 2.1.1, 2.4.3)

- El radiogroup de severidad de síntomas (`SymptomPicker`) declaraba
  `role="radio"` sin navegación por flechas: cada botón era un tab stop aislado
  y el rol prometía un comportamiento que no existía. Se añadieron
  flechas/Home/End con *roving tabindex*, el mismo patrón que ya seguía
  `MoodScale`.
- El `Modal` ya atrapaba y devolvía el foco; se dejó intacto.

### Textos alternativos (WCAG 1.1.1)

- El `<canvas>` del gráfico de tendencias no tenía nombre accesible: un lector
  de pantalla no anunciaba nada o anunciaba "canvas". El contenedor ahora es
  `role="img"` con `aria-label` que lista las series y los días mostrados.
- Verificados como decorativos (`aria-hidden` / `alt=""` donde procede): logo
  de la cabecera, avatar del usuario, iconos, esqueletos de carga y la barra de
  progreso del modal.

### Regiones anunciadas (WCAG 4.1.3, 1.3.1)

- El estado de carga de tendencias no se anunciaba (los esqueletos son
  `aria-hidden` por diseño). Se añadió una región `role="status"` sr-only
  ("Loading your trends…") y `aria-busy` en la sección mientras carga; el error
  pasa a `role="alert"`.
- El cambio de paso en el modal de registro no se anunciaba (el foco queda en
  el botón). La línea "Step X of Y" es ahora una región viva `role="status"`, y
  la barra de progreso es `aria-hidden` para no leer dos veces lo mismo.
- Los títulos de paso bajaron de `h2` a `h3` para respetar la jerarquía bajo el
  título `h2` del diálogo.

## Resultado

axe y Lighthouse con 0 violaciones en las pantallas auditadas. Los tests de
unidad del frontend (179) siguen en verde y el build de producción pasa; el
único test tocado fue el que fijaba el color del botón `primary`, actualizado
para reflejar el tono accesible.

## Cómo repetir la auditoría

```bash
# 1) Stack arriba (frontend en :5173, backend proxado en :3000)
docker compose up -d --build

# 2) Inyectar axe-core en una página renderizada (Playwright), o
npx --yes lighthouse http://localhost:5173/login \
  --only-categories=accessibility --output=json \
  --chrome-flags="--headless=new" --quiet
```