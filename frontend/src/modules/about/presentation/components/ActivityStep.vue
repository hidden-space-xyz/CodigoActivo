<script setup lang="ts">
import type { OrganizationActivity } from '@/modules/about/presentation/composables/useOrganizationContent'

defineProps<{ activity: OrganizationActivity }>()
</script>

<template>
  <div class="activity">
    <div class="activity__badge" :style="{ background: activity.soft, color: activity.color }">
      {{ activity.number }}
    </div>
    <div class="activity__body">
      <div class="activity__title">{{ activity.title }}</div>
      <div class="activity__desc">{{ activity.description }}</div>
    </div>
  </div>
</template>

<style scoped>
.activity {
  display: flex;
  gap: 14px;
  align-items: flex-start;
}

.activity__badge {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  border-radius: 9px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: var(--ca-font-mono);
  font-weight: 600;
  font-size: 14px;
}

.activity__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 17px;
  color: var(--ca-text);
}

.activity__desc {
  font-size: 14.5px;
  line-height: 1.55;
  color: var(--ca-text-dim);
  margin-top: 3px;
}
</style>
