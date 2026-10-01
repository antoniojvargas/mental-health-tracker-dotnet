<script setup lang="ts">
/**
 * Marca de la app: un anillo abierto con un punto en el centro. El hueco evoca
 * la entrada y la salida de aire del ejercicio de respiración, que es lo que
 * sostiene el resto de la interfaz.
 *
 * SVG en línea y `currentColor`, a propósito. Un archivo `.svg` importado como
 * `<img>` llega al DOM por una vía aparte del CSS: no hereda `currentColor` y
 * hay que cambiarle el color con un filtro o con dos archivos. Inline, el mismo
 * marcado sirve para el header, el favicon y el tema oscuro sin duplicarse, y no
 * hay nada que sincronizar.
 */

withDefaults(
  defineProps<{
    /** Alto del logo. La caja es cuadrada siempre. */
    size?: 'sm' | 'md' | 'lg'
    /** Nombre de la marca, para lectores de pantalla. Sin él, el logo es mudo. */
    label?: string
  }>(),
  { size: 'md', label: 'Stillwater' },
)

const SIZES = { sm: 'h-8 w-8', md: 'h-10 w-10', lg: 'h-14 w-14' } as const
</script>

<template>
  <svg
    viewBox="0 0 40 40"
    :class="SIZES[size]"
    :role="label ? 'img' : undefined"
    :aria-label="label"
    :aria-hidden="label ? undefined : 'true'"
    fill="none"
  >
    <!--
      Los comentarios van DENTRO del `<svg>`. En la raíz de un `<template>` uno
      convertiría el componente en un fragmento de varios nodos, y Vue dejaría de
      heredar los atributos al `svg`, que es justo lo que decide si el logo se
      anuncia o no.

      `role="img"` con `aria-label` solo cuando hay etiqueta. Un SVG decorativo es
      `aria-hidden`; uno con nombre necesita un rol, porque un `<svg>` suelto no lo
      anuncia. Es decir: el mismo componente sirve para el logo del header y para el
      adorno de una esquina, y quien lo usa decide si tiene algo que decir.

      El hueco del anillo es `66 22` sobre una circunferencia de ~88: se abre poco
      más de un cuarto de vuelta, arriba a la derecha. Es la dirección de la
      entrada de aire, y por eso está donde está y no centrado.
    -->
    <circle
      cx="20"
      cy="20"
      r="14"
      stroke="currentColor"
      stroke-width="1.5"
      stroke-linecap="round"
      stroke-dasharray="66 22"
      class="text-ink-300"
    />
    <circle cx="20" cy="20" r="5" fill="currentColor" class="text-clearsky-500" />
  </svg>
</template>
