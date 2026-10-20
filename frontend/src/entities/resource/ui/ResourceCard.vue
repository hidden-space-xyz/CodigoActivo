<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import { formatDate } from '@/shared/lib/date'
import { CardDate } from '@/shared/ui/card-date'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import type { LearningResourceSummary } from '../model/types'

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

    <CardDate
      :label="$t('entities.resource.card.dateLabel')"
      :value="formatDate(resource.createdAt)"
    />
    <h3 class="ca-list-card__title">{{ resource.title }}</h3>
    <p v-if="resource.subtitle" class="ca-list-card__subtitle resource-card__subtitle">
      {{ resource.subtitle }}
    </p>
  </component>
</template>

<style scoped>
.resource-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
</style>
