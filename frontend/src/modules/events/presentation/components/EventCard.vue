<script setup lang="ts">
import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'
import ListThumbnail from '@/shared/ui/components/ListThumbnail.vue'

defineProps<{ event: UpcomingEvent }>()
</script>

<template>
  <RouterLink :to="{ name: 'event-detail', params: { eventId: event.id } }" class="event-card">
    <ListThumbnail :thumbnail-id="event.thumbnailId" :alt="event.title" />

    <h3 class="event-card__title">{{ event.title }}</h3>
    <div v-if="event.slogan" class="event-card__slogan">«{{ event.slogan }}»</div>

    <div class="event-card__footer">
      <span class="event-card__status">{{ event.status }}</span>
      <span class="event-card__more">Más info →</span>
    </div>
  </RouterLink>
</template>

<style scoped>
.event-card {
  display: flex;
  flex-direction: column;
  gap: 14px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 16px;
  cursor: pointer;
  text-decoration: none;
  color: inherit;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.event-card:hover {
  transform: translateY(-4px);
  border-color: rgba(255, 255, 255, 0.2);
}

.event-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 21px;
  line-height: 1.15;
  color: var(--ca-text);
}

.event-card__slogan {
  font-size: 15px;
  margin-top: -6px;
  color: var(--ca-cyan);
}

.event-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: auto;
  padding-top: 14px;
  border-top: 1px solid var(--ca-border);
}

.event-card__status {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-green);
}

.event-card__more {
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-text);
}
</style>
