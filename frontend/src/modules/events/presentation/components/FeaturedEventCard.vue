<script setup lang="ts">
import { computed } from 'vue'

import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import { fileContentUrl } from '@/shared/utils/media'

const props = defineProps<{ event: UpcomingEvent }>()

const posterUrl = computed(() => fileContentUrl(props.event.thumbnailId))
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

      <div class="featured__poster">
        <img v-if="posterUrl" :src="posterUrl" :alt="event.title" class="featured__poster-img" />
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
  background: var(--ca-bg-deep);
  border-left: 1px solid var(--ca-border);
}

.featured__poster-img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
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
