<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import type { LearningResourceSummary } from '../model/types'
import { CardDate, ListThumbnail } from '@/shared/ui'

const props = defineProps<{
  /** Resource card; links to `url` as a plain anchor when set, otherwise to the detail route. */
  resource: LearningResourceSummary
}>()

const linkAttrs = computed(() =>
  props.resource.url
    ? { href: props.resource.url }
    : { to: { name: 'resource-detail', params: { resourceId: props.resource.id } } },
)
</script>

<template>
  <component
    :is="resource.url ? 'a' : RouterLink"
    v-bind="linkAttrs"
    class="ca-list-card resource-card"
  >
    <ListThumbnail :thumbnail-id="resource.thumbnailId" :alt="resource.title" />

    <CardDate :label="$t('entities.resource.card.dateLabel')" :value="resource.date" />
    <h3 class="resource-card__title">{{ resource.title }}</h3>
    <p v-if="resource.subtitle" class="resource-card__subtitle">{{ resource.subtitle }}</p>
  </component>
</template>

<style scoped>
.resource-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
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
