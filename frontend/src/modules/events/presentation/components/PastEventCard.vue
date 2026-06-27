<script setup lang="ts">
import type { PastEvent } from '@/modules/events/domain/entities/event.entity'

defineProps<{ event: PastEvent }>()
</script>

<template>
  <article class="past-card">
    <div class="past-card__photo" aria-hidden="true">foto · galería</div>
    <div class="past-card__body">
      <div class="past-card__top">
        <span class="past-card__year">{{ event.year }}</span>
      </div>
      <h3 class="past-card__title">{{ event.title }}</h3>
      <div class="past-card__event">{{ event.eventName }}</div>
    </div>
  </article>
</template>

<style scoped>
.past-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  overflow: hidden;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.past-card:hover {
  transform: translateY(-4px);
  border-color: rgba(255, 255, 255, 0.2);
}

.past-card__photo {
  height: 172px;
  background: repeating-linear-gradient(135deg, #171b25, #171b25 11px, #13161e 11px, #13161e 22px);
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: var(--ca-font-mono);
  color: var(--ca-text-ghost);
  font-size: 12px;
}

.past-card__body {
  padding: 20px;
}

.past-card__top {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  margin-bottom: 12px;
}

.past-card__year {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 15px;
  color: var(--ca-text-faint);
}

.past-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 19px;
  line-height: 1.2;
  color: var(--ca-text);
}

.past-card__event {
  font-size: 14px;
  color: var(--ca-text-dim);
  margin-top: 5px;
}
</style>
