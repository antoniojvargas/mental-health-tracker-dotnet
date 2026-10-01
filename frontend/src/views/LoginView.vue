<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '../stores/useAuthStore'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/**
 * El backend manda aquí con `?error=auth_failed` cuando rechaza el state o el
 * code de Google, y también cuando el intercambio con Google falla
 * (AuthController.RedirectToLogin). Los dos casos comparten el mismo código, así
 * que el texto no puede distinguirlos y no debe fingir que lo hace.
 *
 * Solo se reconoce ese valor exacto: un `error` desconocido se ignora en vez de
 * caer en el mensaje equivocado.
 */
const showAuthError = computed(() => route.query.error === 'auth_failed')

onMounted(async () => {
  try {
    const user = await auth.fetchMe()
    if (user) {
      // `replace` y no `push`: nobody pidió entrar aquí, así que /login no debe
      // quedar en el historial. Con `push`, el botón de atrás devolvería al
      // visitor a /login, el guard lo devolvería a /dashboard, y el usuario
      // pelearía con su propio historial.
      await router.replace('/dashboard')
    }
  } catch {
    // La API no está disponible. La tarjeta de login tiene que seguir siendo
    // usable y no hay a dónde mandar al usuario, así que se queda aquí. El 401
    // no llega a este catch: el store lo resuelve como "no hay sesión", que es
    // justo el caso normal de quien está en esta pantalla.
  }
})
</script>

<template>
  <main class="relative grid min-h-svh place-content-center overflow-hidden bg-paper-100 px-6">
    <!--
      Capa ambiente. Los dos adornos viven en un envoltorio que lleva el
      `aria-hidden` y el `pointer-events-none` una sola vez: ambos son adorno,
      y repetirlo en cada hijo es una forma de que se le olvide a uno.

      `ember` es el único color que aparece aquí, y es su único uso en toda la
      app: la paleta lo reservó para el degradado ambiente del login y lo
      descartó para cromo de interfaz. Al ir detrás de todo y desenfocado se lee
      como luz, no como un elemento.
    -->
    <div aria-hidden="true" class="pointer-events-none absolute inset-0">
      <!-- Banda superior: el lavado ancho que baja desde el borde. -->
      <div class="absolute inset-x-0 top-0 h-96 bg-ember-300/35 blur-3xl"></div>
      <!--
        Círculo cálido arriba a la derecha. Un tono más fuerte que la banda para
        que se lea como una capa por delante de ella y no como una mancha más
        grande; sangra fuera del encuadre a propósito, que es lo que lo hace
        parecer luz que viene de fuera y no un círculo pegado a la esquina.
      -->
      <div class="absolute -top-28 -right-28 h-96 w-96 rounded-full bg-ember-400/40 blur-3xl"></div>
    </div>

    <section
      class="animate-slide-up relative w-full max-w-sm rounded-2xl border border-ink-100 bg-paper-50 px-8 py-10 shadow-sm"
    >
      <!--
        Marca: un anillo abierto con un punto en el centro. El hueco evoca la
        entrada y la salida de aire del ejercicio de respiración, que es lo que
        sostiene el resto de la interfaz. Va en línea y con `currentColor` para
        no poder desincronizarse de la paleta.
      -->
      <svg viewBox="0 0 40 40" class="h-10 w-10" fill="none" aria-hidden="true">
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

      <!-- `display` es la familia de los momentos de marca y los títulos. -->
      <h1 class="font-display mt-6 text-2xl font-semibold text-ink-700">Stillwater</h1>

      <p class="font-sans mt-2 text-sm leading-relaxed text-ink-400">
        A quiet place to keep track of how the days are going.
      </p>

      <!--
        Aviso de fallo de autenticación. Va antes del botón porque explica por
        qué el clic anterior no funcionó, que es justo lo que se busca en ese
        punto de la pantalla.

        `role="alert"` implica `aria-live="assertive"`: un lector de pantalla lo
        interrumpe para leerlo. Sin él el texto se vería igual pero pasaría
        desapercibido a quien no ve.

        Los tonos son de `ink` y no un rojo de error porque la paleta no tiene
        ninguno, y `ember` —el único cálido— está reservado para el degradado
        ambiente. Un aviso en `ink` se lee como una nota serena, que es el
        registro del resto de la aplicación.
      -->
      <p
        v-if="showAuthError"
        role="alert"
        class="font-sans mt-6 rounded-lg border border-ink-200 bg-ink-50 px-4 py-3 text-sm leading-relaxed text-ink-600"
      >
        We couldn't sign you in with Google. Please try again.
      </p>

      <!--
        Un `<a>` y no un router-link a propósito: el flujo de Google se resuelve
        con cookies httpOnly que fija el backend, así que hace falta una
        navegación de página entera. Un enlace del router intentaría resolverlo
        en el cliente y la cookie no llegaría a fijarse.

        El margen superior baja a `mt-4` cuando hay aviso, para que el botón no
        se despegue del texto que lo explica.
      -->
      <a
        href="/api/auth/google"
        :class="[
          'font-sans inline-flex w-full items-center justify-center gap-3 rounded-xl border border-ink-200 bg-paper-50 px-5 py-3 text-sm font-medium text-ink-600 transition-colors hover:border-clearsky-300 hover:bg-clearsky-50 hover:text-ink-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-clearsky-500',
          showAuthError ? 'mt-4' : 'mt-8',
        ]"
      >
        <svg viewBox="0 0 18 18" class="h-4 w-4 shrink-0" aria-hidden="true">
          <path
            fill="#4285F4"
            d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.7-1.57 2.68-3.88 2.68-6.62Z"
          />
          <path
            fill="#34A853"
            d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.81.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.03-3.7H.96v2.33A9 9 0 0 0 9 18Z"
          />
          <path
            fill="#FBBC05"
            d="M3.97 10.72a5.4 5.4 0 0 1 0-3.44V4.95H.96a9 9 0 0 0 0 8.1l3.01-2.33Z"
          />
          <path
            fill="#EA4335"
            d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58C13.46.9 11.43 0 9 0A9 9 0 0 0 .96 4.95l3.01 2.33C4.68 5.16 6.66 3.58 9 3.58Z"
          />
        </svg>
        Continue with Google
      </a>

      <p class="font-sans mt-8 text-xs leading-relaxed text-ink-300">
        We use your Google account to sign in and nothing more. Your entries stay in this app's own
        database and are never sold or shared.
      </p>
    </section>
  </main>
</template>
