<script setup lang="ts">
import { computed } from 'vue'

import { useEventDetail } from '@/modules/events/presentation/composables/useEventDetail'
import { useEventSignup } from '@/modules/events/presentation/composables/useEventSignup'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import { fileContentUrl } from '@/shared/utils/media'

const props = defineProps<{ eventId: string }>()

const { event, isLoading, notFound } = useEventDetail(() => props.eventId)
const { signedUp, signUp } = useEventSignup(() => props.eventId)

const accentColor = 'var(--ca-cyan)'

const infoRows = computed(() =>
  event.value
    ? [
        { label: 'Fecha', value: event.value.dateLabel },
        { label: 'Inscripción', value: event.value.signupLabel },
        { label: 'Estado', value: event.value.status },
      ]
    : [],
)

const posterUrl = computed(() => fileContentUrl(event.value?.thumbnailId))
</script>

<template>
  <div>
    <section class="detail-back">
      <div class="ca-container--narrow">
        <BaseButton variant="link" :to="{ name: 'events' }"> ← Volver a eventos </BaseButton>
      </div>
    </section>

    <p v-if="isLoading" class="detail-state ca-container--narrow">Cargando…</p>

    <p v-else-if="notFound || !event" class="detail-state ca-container--narrow">
      No hemos encontrado ese evento.
    </p>

    <template v-else>
      <section class="detail-head">
        <div class="ca-container--narrow">
          <h1 class="detail-head__title">{{ event.title }}</h1>
          <div v-if="event.subtitle" class="detail-head__slogan" :style="{ color: accentColor }">
            «{{ event.subtitle }}»
          </div>
        </div>
      </section>

      <section class="detail-body">
        <div class="ca-container--narrow detail-body__grid">
          <div class="detail-body__main">
            <img
              v-if="posterUrl"
              :src="posterUrl"
              :alt="event.title"
              class="detail-body__poster"
            />

            <h2 class="detail-body__h2">Sobre el evento</h2>
            <p v-if="event.description" class="detail-body__p">{{ event.description }}</p>
            <p v-else class="detail-body__p detail-body__p--muted">
              Este evento todavía no tiene una descripción.
            </p>
          </div>

          <aside class="detail-body__panel">
            <h3 class="detail-panel__title">Información</h3>
            <dl class="detail-panel__info">
              <div v-for="row in infoRows" :key="row.label" class="detail-panel__row">
                <dt class="detail-panel__label">{{ row.label }}</dt>
                <dd class="detail-panel__value">{{ row.value }}</dd>
              </div>
            </dl>
            <div v-if="signedUp" class="detail-panel__signed">
              <span class="detail-panel__check" aria-hidden="true">✓</span>
              ¡Te has apuntado a {{ event.title }}!
            </div>
            <template v-else>
              <p class="detail-panel__note">¿Quieres participar? Apúntate a este evento.</p>
              <BaseButton variant="primary" block @click="signUp"> Apúntate </BaseButton>
            </template>
          </aside>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.detail-back {
  padding: 36px 24px 0;
}

.detail-state {
  padding: 40px 24px;
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.detail-head {
  padding: 24px 24px 24px;
}

.detail-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  line-height: 1.05;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
  margin-top: 16px;
}

.detail-head__slogan {
  font-family: var(--ca-font-display);
  font-size: 23px;
  margin-top: 8px;
}

.detail-body {
  padding: 24px 24px 80px;
}

.detail-body__grid {
  display: grid;
  grid-template-columns: 1.3fr 0.7fr;
  gap: 40px;
  align-items: start;
}

.detail-body__poster {
  width: 100%;
  height: auto;
  border-radius: 18px;
  border: 1px solid var(--ca-border);
  margin-bottom: 28px;
  display: block;
}

.detail-body__h2 {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.detail-body__p {
  margin-top: 12px;
  font-size: 16.5px;
  line-height: 1.7;
  color: #b6bdca;
  white-space: pre-line;
}

.detail-body__p--muted {
  color: var(--ca-text-dim);
  font-style: italic;
}

.detail-body__panel {
  position: sticky;
  top: 90px;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 26px;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.4);
}

.detail-panel__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 19px;
  color: var(--ca-text-bright);
  margin-bottom: 16px;
}

.detail-panel__info {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.detail-panel__row {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.detail-panel__label {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.detail-panel__value {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
}

.detail-panel__note {
  margin: 22px 0 16px;
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text-dim);
}

.detail-panel__signed {
  margin-top: 22px;
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: rgba(91, 229, 132, 0.12);
  border: 1px solid var(--ca-green);
  border-radius: 12px;
  padding: 16px;
  font-size: 14.5px;
  line-height: 1.45;
  color: var(--ca-text);
}

.detail-panel__check {
  color: var(--ca-green);
  font-weight: 700;
}

@media (max-width: 860px) {
  .detail-body__grid {
    grid-template-columns: 1fr;
  }
  .detail-body__panel {
    position: static;
  }
}
</style>
