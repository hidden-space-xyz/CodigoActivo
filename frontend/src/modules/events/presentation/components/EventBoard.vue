<script setup lang="ts">
import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'

import EventCard from './EventCard.vue'

defineProps<{ events: readonly UpcomingEvent[] }>()
</script>

<template>
  <div class="board">
    <EventCard v-for="event in events" :key="event.id" :event="event" />
  </div>
</template>

<style scoped>
.board {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(330px, 1fr));
  gap: 22px;
}
</style>
