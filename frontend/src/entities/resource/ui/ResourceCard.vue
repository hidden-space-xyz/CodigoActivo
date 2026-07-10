<script setup lang="ts">
import type { LearningResourceSummary } from '../model/types'
import { ListThumbnail } from '@/shared/ui'

defineProps<{ resource: LearningResourceSummary }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'resource-detail', params: { resourceId: resource.id } }"
    class="resource-card"
  >
    <ListThumbnail :thumbnail-id="resource.thumbnailId" :alt="resource.title" />

    <h3 class="resource-card__title">{{ resource.title }}</h3>
    <p v-if="resource.type" class="resource-card__subtitle">{{ resource.type }}</p>
  </RouterLink>
</template>

<style scoped>
.resource-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
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

.resource-card:hover {
  transform: translateY(-4px);
  border-color: var(--ca-border-strong);
}

.resource-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.resource-card__subtitle {
  font-size: 14.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}
</style>
