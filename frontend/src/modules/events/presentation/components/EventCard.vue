<script setup lang="ts">
import type { UpcomingEvent } from '@/modules/events/domain/entities/event.entity'

defineProps<{ event: UpcomingEvent }>()
</script>

<template>
  <RouterLink :to="{ name: 'event-detail', params: { eventId: event.id } }" class="event-card">
    <div class="event-card__top">
      <div class="event-card__date">
        <span class="event-card__day">{{ event.dayLabel }}</span>
        <span class="event-card__month">{{ event.monthLabel }}</span>
      </div>
    </div>

    <h3 class="event-card__title">{{ event.title }}</h3>
    <div class="event-card__slogan">«{{ event.slogan }}»</div>

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
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 24px;
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

.event-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
}

.event-card__date {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 54px;
  height: 54px;
  border-radius: 12px;
  border: 1px solid var(--ca-cyan);
  background: rgba(45, 212, 217, 0.14);
}

.event-card__day {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 20px;
  line-height: 1;
  color: var(--ca-cyan);
}

.event-card__month {
  font-family: var(--ca-font-mono);
  font-size: 10px;
  letter-spacing: 0.05em;
  color: var(--ca-cyan);
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
  margin-top: 4px;
  color: var(--ca-cyan);
}

.event-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: auto;
  padding-top: 16px;
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
