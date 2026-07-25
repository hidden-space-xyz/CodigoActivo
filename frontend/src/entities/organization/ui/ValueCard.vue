<script setup lang="ts">
import type { OrganizationValue } from '../model/content'

defineProps<{ value: OrganizationValue }>()
</script>

<template>
  <div class="value-card">
    <div class="value-card__icon" :style="{ background: value.soft }">{{ value.icon }}</div>
    <div class="value-card__title">{{ value.title }}</div>
    <div class="value-card__desc">{{ value.description }}</div>
  </div>
</template>

<style scoped>
.value-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 26px;
}

.value-card__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 42px;
  height: 42px;
  border-radius: 11px;
  font-size: 20px;
  margin-bottom: 16px;
}

.value-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 19px;
  color: var(--ca-text);
}

.value-card__desc {
  font-size: 15px;
  line-height: 1.55;
  color: var(--ca-text-dim);
  margin-top: 8px;
}
</style>
