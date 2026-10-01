<script setup lang="ts">
/**
 * Esqueleto de carga. Un bloque del color del papel, pulsando muy despacio.
 *
 * No es un spinner: el spinner dice "aquí hay algo, todavía no". El esqueleto
 * dice "esto es lo que hay aquí, todavía sin contenido", y evita el salto de
 * layout que se produce cuando el contenido real llega con otra altura.
 */

/**
 * Alto del bloque. Se escribe como clase y no como `height` en píxeles para que
 * el esqueleto herede la misma escala de espaciado que el resto de la app, y
 * para que un `h-` cualquiera de Tailwind sustituya a uno escrito a mano.
 */
withDefaults(defineProps<{ rounded?: 'sm' | 'md' | 'lg' | 'full' }>(), { rounded: 'md' })

const RADIUS = {
  sm: 'rounded-sm',
  md: 'rounded-md',
  lg: 'rounded-lg',
  full: 'rounded-full',
} as const
</script>

<template>
  <div aria-hidden="true" :class="['bg-paper-200 animate-pulse', RADIUS[rounded]]" v-bind="$attrs">
    <!--
      Este comentario va DENTRO del div y no antes, a propósito. Un comentario en
      la raíz de un `<template>` convierte el componente en un fragmento de varios
      nodos, y Vue deja de heredar los atributos al elemento: `$attrs` se aplicaría
      al primero que encuentre. Y `$attrs` es justamente lo que hace que este
      bloque acepte un `h-4 w-32` desde fuera, así que el comentario tiene que ir
      dentro.

      `aria-hidden`: es decoración. Lo que hay que anunciar cuando los datos están
      cargando es "cargando", y eso lo dice la región que contiene al esqueleto, no
      cada bloque. Un esqueleto legible por lector de pantalla recita un montón de
      bloques vacíos y encima tapa el texto real que va a aparecer ahí.
    -->
  </div>
</template>
