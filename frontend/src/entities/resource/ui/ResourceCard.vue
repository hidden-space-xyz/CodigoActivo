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

    <div class="resource-card__footer">
      <span class="resource-card__type">{{ resource.type }}</span>
      <span class="resource-card__open">Abrir →</span>
    </div>
  </RouterLink>
</template>

<style scoped>
.resource-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  cursor: pointer;
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

.resource-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: auto;
  padding-top: 14px;
  border-top: 1px solid var(--ca-border);
}

.resource-card__type {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
}

.resource-card__open {
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-orange-ink);
}
</style>
