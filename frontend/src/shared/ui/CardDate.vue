<script setup lang="ts">
import AppIcon from './AppIcon.vue'

defineProps<{
  label: string
  value: string
}>()
</script>

<template>
  <time v-if="value" class="card-date">
    <AppIcon name="calendar" class="card-date__icon" />
    <span class="card-date__label">{{ label }}:</span>
    <span class="card-date__value">{{ value }}</span>
  </time>
</template>

<style scoped>
.card-date {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
  color: var(--ca-text-muted);
  font-size: 13px;
  line-height: 1.35;
}

.card-date__icon {
  flex: 0 0 auto;
  color: var(--ca-orange-ink);
  font-size: 14px;
}

.card-date__label {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.card-date__value {
  color: var(--ca-text);
  font-weight: 600;
}
</style>
