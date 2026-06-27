<script setup lang="ts">
import type { OrganizationValue } from '@/modules/about/presentation/composables/useOrganizationContent'

withDefaults(defineProps<{ value: OrganizationValue; size?: 'sm' | 'md' }>(), { size: 'md' })
</script>

<template>
  <div class="value-card" :class="`value-card--${size}`">
    <div class="value-card__icon" :style="{ background: value.soft }">{{ value.icon }}</div>
    <div class="value-card__title">{{ value.title }}</div>
    <div class="value-card__desc">{{ value.description }}</div>
  </div>
</template>

<style scoped>
.value-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  padding: 20px;
}

.value-card--md {
  border-radius: 16px;
  padding: 26px;
}

.value-card__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: 9px;
  font-size: 17px;
  margin-bottom: 12px;
}

.value-card--md .value-card__icon {
  width: 42px;
  height: 42px;
  border-radius: 11px;
  font-size: 20px;
  margin-bottom: 16px;
}

.value-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 16px;
  color: var(--ca-text);
}

.value-card--md .value-card__title {
  font-size: 19px;
}

.value-card__desc {
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text-dim);
  margin-top: 4px;
}

.value-card--md .value-card__desc {
  font-size: 15px;
  line-height: 1.55;
  margin-top: 8px;
}
</style>
