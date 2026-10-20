<script setup lang="ts">
import { formatDate } from '@/shared/lib/date'
import { CardDate } from '@/shared/ui/card-date'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import type { NewsSummary } from '../model/types'

defineProps<{
  /** News item rendered as a list card that links to its detail page. */
  newsItem: NewsSummary
}>()
</script>

<template>
  <RouterLink
    :to="{ name: 'news-detail', params: { newsItemId: newsItem.id } }"
    class="ca-list-card news-card"
  >
    <ListThumbnail :thumbnail-id="newsItem.thumbnailId" :alt="newsItem.title" />

    <CardDate
      :label="$t('entities.newsItem.card.dateLabel')"
      :value="formatDate(newsItem.createdAt)"
    />
    <h3 class="ca-list-card__title">{{ newsItem.title }}</h3>
    <p v-if="newsItem.subtitle" class="ca-list-card__subtitle news-card__subtitle">
      {{ newsItem.subtitle }}
    </p>
  </RouterLink>
</template>

<style scoped>
.news-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
</style>
