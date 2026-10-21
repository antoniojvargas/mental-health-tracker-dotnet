<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import MetricSelector from '../components/charts/MetricSelector.vue'
import RangeToggle from '../components/charts/RangeToggle.vue'
import TrendChart from '../components/charts/TrendChart.vue'
import DailyLogModal from '../components/daily-log/DailyLogModal.vue'
import Button from '../components/ui/Button.vue'
import Logo from '../components/ui/Logo.vue'
import Skeleton from '../components/ui/Skeleton.vue'
import { useLogSocket } from '../composables/useLogSocket'
import { useAuthStore } from '../stores/useAuthStore'
import { useLogsStore, type LogRange } from '../stores/useLogsStore'
import type { DailyLog } from '../types/daily-log'
import { todayIso } from '../utils/date'

const auth = useAuthStore()
const logsStore = useLogsStore()
const router = useRouter()

/** Ventana de tendencias: la fija `RangeToggle` y dispara la recarga. */
const range = ref<LogRange>('week')

/** Métricas dibujadas; `MetricSelector` deja activar hasta tres. */
const metrics = ref<string[]>(['mood', 'anxiety', 'sleepHours'])
const modalOpen = ref(false)

/**
 * Una sola carga, la inicial incluida: `immediate` cubre el montaje y el mismo
 * manejador cubre cada cambio de ventana, sin duplicar la petición.
 */
watch(range, (value) => void logsStore.fetch(value), { immediate: true })

/**
 * Los registros que llegan por el hub (guardados en otra pestaña) entran por
 * `mergeLog`: si el día ya estaba en la lista se reemplaza, y si no se inserta
 * en orden. Así el panel se actualiza sin recargar. Si la conexión se corta y
 * vuelve, se relee la ventana para recuperar lo que se emitió mientras tanto.
 */
useLogSocket(
  (log) => logsStore.mergeLog(log),
  // El hub no reenvía lo emitido durante un corte: al volver, la API manda.
  () => void logsStore.fetch(range.value),
)

/**
 * El avatar es opcional: Google no siempre entrega una foto, así que la
 * cabecera se queda con el logo y el botón cuando no hay ninguna. El `v-if` va
 * sobre un valor ya resuelto y no sobre `auth.user`, que Vue no estrecha al
 * leerlo dos veces en la plantilla.
 */
const avatarUrl = computed(() => auth.user?.avatarUrl ?? '')

/** El primer nombre para el saludo; sin sesión, no hay nombre que saludar. */
const firstName = computed(() => auth.user?.name.split(' ')[0] ?? '')

/**
 * El registro de hoy, sacado de los logs ya cargados: como toda ventana
 * (semana o mes) incluye hoy, no hace falta una petición aparte y el dato se
 * mantiene solo cuando la lista cambia tras un guardado.
 *
 * Mientras carga vale `undefined` —distinto de `null`, que es "no hay registro"—,
 * para que la acción no anuncie "ajustar" sobre un dato que aún no llegó.
 */
const todayLog = computed(() => {
  if (logsStore.loading) return undefined
  return logsStore.logs.find((log) => log.logDate === todayIso()) ?? null
})

const dailyMessage = computed(() => {
  // La carga y el fallo van antes que el dato: mientras no sepamos si hay
  // registro de hoy, decir "¿cómo te sientes?" daría por hecho algo que no es
  // cierto (a lo mejor ya lo registraste). Solo cuando la carga termina bien se
  // decide entre invitar a registrarlo o a ajustarlo.
  if (logsStore.loading) return "We're checking your day…"
  if (logsStore.error) return "We couldn't check your day just now."
  return todayLog.value
    ? "You already logged how you're feeling today. You can adjust it if something changed."
    : 'How are you feeling today? Take a minute to log it.'
})

const dailyAction = computed(() => (todayLog.value ? "Adjust today's entry" : 'Log my day'))

/**
 * Reintenta la carga de la ventana activa. Repetir la petición es lo único que
 * puede cambiar el resultado, así que el estado de error de las tendencias
 * siempre ofrece esta salida en vez de dejar al usuario mirando la pared. Como
 * ambas secciones salen del mismo store, reintentar aquí refresca también el
 * saludo diario.
 */
function retry(): void {
  void logsStore.fetch(range.value)
}

/**
 * El modal se cierra solo tras guardar. El POST ya devuelve el registro, así que
 * se integra en la lista con `mergeLog` en vez de volver a pedir la ventana: el
 * saludo y las gráficas lo ven sin una segunda petición.
 */
function handleSaved(log: DailyLog): void {
  logsStore.mergeLog(log)
}

/**
 * Cerrar sesión vacía el store, pero el guard solo mira al navegar: sin este
 * `replace` la pantalla se quedaría con los datos de una sesión que ya no
 * existe. `replace` y no `push` para que el botón de atrás no vuelva aquí.
 *
 * Si la petición falla, la cookie sigue viva en el servidor y no hay a dónde
 * mandar al usuario, así que se le deja donde está.
 */
async function handleLogout(): Promise<void> {
  try {
    await auth.logout()
    await router.replace('/login')
  } catch {
    /* La API no respondió; se reintentará con otro clic. */
  }
}
</script>

<template>
  <div class="min-h-screen bg-paper-100 pb-16">
    <header class="border-b border-ink-100 bg-paper-50/70 backdrop-blur">
      <div class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3">
        <div class="flex items-center gap-2.5">
          <!-- El texto de al lado ya nombra la marca, así que el logo es adorno. -->
          <Logo size="sm" label="" />
          <span class="font-display text-sm font-semibold tracking-wide text-ink-700">
            Stillwater
          </span>
        </div>
        <div class="flex items-center gap-3">
          <img
            v-if="avatarUrl"
            :src="avatarUrl"
            alt=""
            class="h-8 w-8 rounded-full"
            referrerpolicy="no-referrer"
          />
          <Button variant="ghost" class="min-h-11 px-3! py-1.5!" @click="handleLogout">
            Log out
          </Button>
        </div>
      </div>
    </header>

    <main class="mx-auto max-w-3xl space-y-6 px-4 py-8">
      <section
        aria-labelledby="daily-log-heading"
        class="animate-fade-in rounded-2xl border border-ink-100 bg-paper-50 p-6"
      >
        <h1 id="daily-log-heading" class="font-display text-xl font-semibold text-ink-700">
          {{ firstName ? `Hi, ${firstName}.` : 'Hi.' }}
        </h1>
        <p class="mt-1 text-sm text-ink-500">{{ dailyMessage }}</p>
        <Button class="mt-4" @click="modalOpen = true">{{ dailyAction }}</Button>
      </section>

      <section
        aria-labelledby="trends-heading"
        :aria-busy="logsStore.loading"
        class="animate-fade-in rounded-2xl border border-ink-100 bg-paper-50 p-6"
      >
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <h2 id="trends-heading" class="font-display font-semibold text-ink-700">Your trends</h2>
          <RangeToggle v-model="range" />
        </div>
        <MetricSelector v-model="metrics" />
        <!--
          Región viva para el estado de carga. El `Skeleton` es decoración
          (`aria-hidden`), así que sin este anuncio quien no ve la pantalla no
          recibe ninguna señal de que los datos están en camino.
        -->
        <p class="sr-only" role="status">
          {{ logsStore.loading ? "We're getting your trends ready…" : '' }}
        </p>
        <div class="mt-4">
          <Skeleton v-if="logsStore.loading" class="h-72 w-full" />
          <div
            v-else-if="logsStore.error"
            class="flex h-72 flex-col items-center justify-center gap-3 rounded-2xl border border-ink-100 bg-paper-50 px-6 text-center"
          >
            <p role="alert" class="text-sm leading-relaxed text-ink-600">
              {{ logsStore.error }}
            </p>
            <Button variant="subtle" @click="retry">Try again</Button>
          </div>
          <TrendChart v-else :logs="logsStore.logs" :metric-keys="metrics" />
        </div>
      </section>
    </main>

    <DailyLogModal :open="modalOpen" @close="modalOpen = false" @saved="handleSaved" />
  </div>
</template>
