<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import DailyLogModal from '../components/daily-log/DailyLogModal.vue'
import Button from '../components/ui/Button.vue'
import Logo from '../components/ui/Logo.vue'
import { useAuthStore } from '../stores/useAuthStore'
import { useLogsStore, type LogRange } from '../stores/useLogsStore'
import type { IsoDate } from '../types/daily-log'

const auth = useAuthStore()
const logsStore = useLogsStore()
const router = useRouter()

/**
 * Ventana de tendencias. Por ahora solo decide la carga inicial; `RangeToggle`
 * la moverá desde la sección de tendencias.
 */
const range = ref<LogRange>('week')
const modalOpen = ref(false)

onMounted(() => {
  void logsStore.fetch(range.value)
})

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
 * "Hoy" en hora local, que es la que usa el backend al guardar `logDate`.
 *
 * No sirve `toISOString()`: pasa a UTC y en un huso negativo devolvería el día
 * siguiente por la noche, que es justo el error que documenta `logs.api`.
 */
function todayIso(): IsoDate {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

/**
 * El registro de hoy, sacado de los logs ya cargados: como toda ventana
 * (semana o mes) incluye hoy, no hace falta una petición aparte y el dato se
 * mantiene solo cuando la lista cambia tras un guardado.
 *
 * Mientras carga vale `undefined`, que la sección distingue de `null` ("no hay
 * registro") para poder mostrar su propio estado de espera.
 */
const todayLog = computed(() => {
  if (logsStore.loading) return undefined
  return logsStore.logs.find((log) => log.logDate === todayIso()) ?? null
})

const dailyMessage = computed(() => {
  if (todayLog.value === undefined) return 'Checking your day…'
  return todayLog.value
    ? "You already logged how you're feeling today. You can adjust it if something changed."
    : 'How are you feeling today? Take a minute to log it.'
})

const dailyAction = computed(() => (todayLog.value ? "Adjust today's entry" : 'Log my day'))

/**
 * El modal se cierra solo tras guardar; aquí solo hay que releer la ventana
 * para que el saludo y las gráficas vean el registro nuevo.
 */
function handleSaved(): void {
  void logsStore.fetch(range.value)
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
          <Button variant="ghost" class="px-3! py-1.5!" @click="handleLogout">Log out</Button>
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
        class="animate-fade-in rounded-2xl border border-ink-100 bg-paper-50 p-6"
      >
        <h2 id="trends-heading" class="font-display font-semibold text-ink-700">Your trends</h2>
      </section>
    </main>

    <DailyLogModal :open="modalOpen" @close="modalOpen = false" @saved="handleSaved" />
  </div>
</template>
