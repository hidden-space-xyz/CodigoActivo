<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import type { LearningResourceSummary } from '../model/types'
import { ColorTag, ListThumbnail } from '@/shared/ui'

const props = defineProps<{ resource: LearningResourceSummary }>()

const linkAttrs = computed(() =>
  props.resource.url
    ? { href: props.resource.url }
    : { to: { name: 'resource-detail', params: { resourceId: props.resource.id } } },
)
</script>

<template>
  <component :is="resource.url ? 'a' : RouterLink" v-bind="linkAttrs" class="resource-card">
    <ListThumbnail :thumbnail-id="resource.thumbnailId" :alt="resource.title" />

    <ColorTag
      v-if="resource.typeName"
      class="resource-card__type"
      :value="resource.typeName"
      :color="resource.typeColor"
    />
    <h3 class="resource-card__title">{{ resource.title }}</h3>
    <p v-if="resource.subtitle" class="resource-card__subtitle">{{ resource.subtitle }}</p>
  </component>
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

.resource-card__type {
  align-self: flex-start;
  margin-top: 4px;
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
