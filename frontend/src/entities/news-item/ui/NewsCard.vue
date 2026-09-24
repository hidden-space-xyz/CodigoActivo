<script setup lang="ts">
import type { NewsSummary } from '../model/types'
import { CardDate, ListThumbnail } from '@/shared/ui'

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

    <CardDate :label="$t('entities.newsItem.card.dateLabel')" :value="newsItem.date" />
    <h3 class="news-card__title">{{ newsItem.title }}</h3>
    <p v-if="newsItem.subtitle" class="news-card__subtitle">
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

.news-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.news-card__subtitle {
  font-size: 14.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}
</style>
