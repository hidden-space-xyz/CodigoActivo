<script setup lang="ts">
import type { PastEvent } from '../model/types'
import { CardDate, ColorTag, ListThumbnail } from '@/shared/ui'
import EventCardFooter from './EventCardFooter.vue'

defineProps<{ event: PastEvent }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'event-detail', params: { eventId: event.id } }"
    class="ca-list-card past-card"
  >
    <ListThumbnail :thumbnail-id="event.thumbnailId" :alt="event.title" />

    <CardDate :label="$t('entities.event.card.dateLabel')" :value="event.date" />
    <h3 class="past-card__title">{{ event.title }}</h3>
    <div v-if="event.eventName" class="past-card__event">«{{ event.eventName }}»</div>
    <div v-if="event.categories.length" class="past-card__cats">
      <ColorTag
        v-for="cat in event.categories"
        :key="cat.id"
        :value="cat.name"
        :color="cat.color"
      />
    </div>

    <EventCardFooter :status="event.status" />
  </RouterLink>
</template>

<style scoped>
.past-card {
  display: flex;
  flex-direction: column;
  gap: 14px;
  cursor: pointer;
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
  margin-top: -6px;
  color: var(--ca-text-muted);
}

.past-card__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 10px;
}
</style>
