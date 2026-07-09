<script setup lang="ts">
import type { PastEvent } from '../model/types'
import { ColorTag, ListThumbnail } from '@/shared/ui'

defineProps<{ event: PastEvent }>()
</script>

<template>
  <RouterLink :to="{ name: 'event-detail', params: { eventId: event.id } }" class="past-card">
    <ListThumbnail :thumbnail-id="event.thumbnailId" :alt="event.title" />
    <div class="past-card__body">
      <div class="past-card__top">
        <span class="past-card__year">{{ event.year }}</span>
      </div>
      <h3 class="past-card__title">{{ event.title }}</h3>
      <div class="past-card__event">{{ event.eventName }}</div>
      <div v-if="event.categories.length" class="past-card__cats">
        <ColorTag
          v-for="cat in event.categories"
          :key="cat.id"
          :value="cat.name"
          :color="cat.color"
        />
      </div>
    </div>
  </RouterLink>
</template>

<style scoped>
.past-card {
  display: block;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 16px;
  text-decoration: none;
  color: inherit;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.past-card:hover {
  transform: translateY(-4px);
  border-color: var(--ca-border-strong);
}

.past-card__body {
  padding: 14px 4px 4px;
}

.past-card__top {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  margin-bottom: 8px;
}

.past-card__year {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
}

.past-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.past-card__event {
  font-size: 14.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  margin-top: 5px;
}

.past-card__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 10px;
}
</style>
