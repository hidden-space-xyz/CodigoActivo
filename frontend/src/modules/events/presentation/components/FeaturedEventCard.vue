<script setup lang="ts">
import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import BaseButton from '@/shared/ui/components/BaseButton.vue'

defineProps<{ event: UpcomingEvent }>()
</script>

<template>
  <div class="featured">
    <div class="featured__grid">
      <div class="featured__body">
        <span class="featured__badge"> ★ Evento destacado </span>

        <h2 class="featured__title">{{ event.title }}</h2>
        <div class="featured__slogan">«{{ event.slogan }}»</div>

        <div class="featured__meta">
          <div class="featured__meta-item">
            <span class="featured__meta-label">Fecha</span>
            <span class="featured__meta-value">{{ event.date }}</span>
          </div>
        </div>

        <BaseButton
          :to="{ name: 'event-detail', params: { eventId: event.id } }"
          variant="light"
          class="featured__cta"
        >
          Ver detalles →
        </BaseButton>
      </div>

      <div class="featured__poster" aria-hidden="true">
        <div class="featured__poster-inner">
          <div class="featured__poster-emoji">🍞</div>
          cartel del evento<br />1600 × 1200
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.featured {
  position: relative;
  overflow: hidden;
  border-radius: 20px;
  border: 1px solid var(--ca-cyan);
  background: rgba(45, 212, 217, 0.14);
}

.featured__grid {
  display: grid;
  grid-template-columns: 1.15fr 0.85fr;
  align-items: stretch;
}

.featured__body {
  padding: 40px 44px;
}

.featured__badge {
  display: inline-block;
  font-family: var(--ca-font-mono);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.09em;
  text-transform: uppercase;
  color: var(--ca-cyan);
  background: rgba(12, 14, 19, 0.45);
  padding: 5px 11px;
  border-radius: 999px;
  margin-bottom: 18px;
}

.featured__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 40px;
  line-height: 1.05;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
}

.featured__slogan {
  font-family: var(--ca-font-display);
  font-size: 21px;
  margin-top: 8px;
  color: var(--ca-cyan);
}

.featured__meta {
  display: flex;
  gap: 22px;
  margin-top: 24px;
  flex-wrap: wrap;
}

.featured__meta-item {
  display: flex;
  flex-direction: column;
}

.featured__meta-label {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.featured__meta-value {
  font-weight: 600;
  color: var(--ca-text);
}

.featured__cta {
  margin-top: 28px;
  font-size: 16px;
  padding: 13px 24px;
  border-radius: 11px;
}

.featured__poster {
  position: relative;
  min-height: 340px;
  background: repeating-linear-gradient(135deg, #171b25, #171b25 11px, #13161e 11px, #13161e 22px);
  display: flex;
  align-items: center;
  justify-content: center;
  border-left: 1px solid var(--ca-border);
}

.featured__poster-inner {
  text-align: center;
  font-family: var(--ca-font-mono);
  color: var(--ca-text-ghost);
  font-size: 13px;
}

.featured__poster-emoji {
  font-size: 30px;
  margin-bottom: 8px;
}

@media (max-width: 860px) {
  .featured__grid {
    grid-template-columns: 1fr;
  }
  .featured__body {
    padding: 32px 28px;
  }
  .featured__poster {
    min-height: 220px;
    border-left: none;
    border-top: 1px solid var(--ca-border);
  }
}
</style>
