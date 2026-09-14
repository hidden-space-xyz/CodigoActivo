<script setup lang="ts">
import type { UpcomingEvent } from '../model/types'
import { CardDate, ColorTag, ListThumbnail } from '@/shared/ui'
import EventCardFooter from './EventCardFooter.vue'

defineProps<{ event: UpcomingEvent }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'event-detail', params: { eventId: event.id } }"
    class="ca-list-card event-card"
  >
    <ListThumbnail :thumbnail-id="event.thumbnailId" :alt="event.title" />

    <CardDate :label="$t('entities.event.card.dateLabel')" :value="event.date" />
    <h3 class="event-card__title">{{ event.title }}</h3>
    <div v-if="event.slogan" class="event-card__slogan">«{{ event.slogan }}»</div>

    <div v-if="event.categories.length" class="event-card__cats">
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
.event-card {
  display: flex;
  flex-direction: column;
  gap: 14px;
  cursor: pointer;
}

.event-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.event-card__slogan {
  font-size: 14.5px;
  line-height: 1.5;
  margin-top: -6px;
  color: var(--ca-text-muted);
}

.event-card__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
</style>
