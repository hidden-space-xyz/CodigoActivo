<script setup lang="ts">
import { computed, ref } from 'vue'

import { useEventDetail } from '@/entities/event'
import EventActivitiesTimeline from './EventActivitiesTimeline.vue'
import { BaseButton, ColorTag, RichTextContent } from '@/shared/ui'
import { fileContentUrl, isRichTextEmpty } from '@/shared/lib'

const props = defineProps<{ eventId: string }>()

const { event, isLoading, notFound } = useEventDetail(() => props.eventId)

const tab = ref<'info' | 'activities'>('info')
const accentColor = 'var(--ca-cyan)'

const hasDescription = computed(() => !isRichTextEmpty(event.value?.description))

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
          <div v-if="event.categories.length" class="detail-head__cats">
            <ColorTag
              v-for="cat in event.categories"
              :key="cat.id"
              :value="cat.name"
              :color="cat.color"
            />
          </div>
        </div>
      </section>

      <nav class="detail-tabs">
        <div class="ca-container--narrow detail-tabs__inner">
          <button
            type="button"
            class="detail-tab"
            :class="{ 'detail-tab--active': tab === 'info' }"
            title="Ver información"
            @click="tab = 'info'"
          >
            Información
          </button>
          <button
            type="button"
            class="detail-tab"
            :class="{ 'detail-tab--active': tab === 'activities' }"
            title="Ver actividades"
            @click="tab = 'activities'"
          >
            Actividades
          </button>
        </div>
      </nav>

      <section v-if="tab === 'info'" class="detail-body">
        <div class="ca-container--narrow detail-body__grid">
          <div class="detail-body__main">
            <img v-if="posterUrl" :src="posterUrl" :alt="event.title" class="detail-body__poster" />

            <h2 class="detail-body__h2">Sobre el evento</h2>
            <RichTextContent v-if="hasDescription" :content="event.description" />
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
            <p class="detail-panel__note">Apúntate a las actividades de este evento.</p>
            <BaseButton variant="primary" block @click="tab = 'activities'">
              Ver actividades
            </BaseButton>
          </aside>
        </div>
      </section>

      <section v-else class="detail-body">
        <div class="ca-container--narrow">
          <EventActivitiesTimeline :event-id="eventId" :signup-open="event.signupOpen" />
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
  padding: 24px 24px 16px;
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

.detail-head__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 14px;
}

.detail-tabs {
  padding: 0 24px;
  border-bottom: 1px solid var(--ca-border);
}

.detail-tabs__inner {
  display: flex;
  gap: 8px;
}

.detail-tab {
  position: relative;
  background: transparent;
  border: none;
  padding: 12px 6px;
  margin-right: 18px;
  font-family: var(--ca-font-display);
  font-size: 16px;
  font-weight: 600;
  color: var(--ca-text-muted);
  cursor: pointer;
  transition: color 0.15s ease;
}

.detail-tab:hover {
  color: var(--ca-text);
}

.detail-tab--active {
  color: var(--ca-text-bright);
}

.detail-tab--active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  border-radius: 2px;
  background: var(--ca-cyan);
}

.detail-body {
  padding: 28px 24px 80px;
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

@media (max-width: 860px) {
  .detail-body__grid {
    grid-template-columns: 1fr;
  }
  .detail-body__panel {
    position: static;
  }
}
</style>
