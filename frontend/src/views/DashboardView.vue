<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'

import Button from '../components/ui/Button.vue'
import Logo from '../components/ui/Logo.vue'
import { useAuthStore } from '../stores/useAuthStore'

const auth = useAuthStore()
const router = useRouter()

/**
 * El avatar es opcional: Google no siempre entrega una foto, así que la
 * cabecera se queda con el logo y el botón cuando no hay ninguna. El `v-if` va
 * sobre un valor ya resuelto y no sobre `auth.user`, que Vue no estrecha al
 * leerlo dos veces en la plantilla.
 */
const avatarUrl = computed(() => auth.user?.avatarUrl ?? '')

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
        aria-label="Daily log"
        class="animate-fade-in rounded-2xl border border-ink-100 bg-paper-50 p-6"
      ></section>

      <section
        aria-labelledby="trends-heading"
        class="animate-fade-in rounded-2xl border border-ink-100 bg-paper-50 p-6"
      >
        <h2 id="trends-heading" class="font-display font-semibold text-ink-700">Your trends</h2>
      </section>
    </main>
  </div>
</template>
