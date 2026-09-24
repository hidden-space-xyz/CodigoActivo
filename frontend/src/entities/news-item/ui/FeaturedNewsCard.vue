<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { NewsSummary } from '../model/types'
import { FeaturedCard } from '@/shared/ui'

const props = defineProps<{
  /** News item shown as the large home highlight; the date row is hidden if `date` is empty. */
  newsItem: NewsSummary
}>()

const { t } = useI18n()

const meta = computed(() =>
  props.newsItem.date
    ? [
        {
          label: t('entities.newsItem.featured.publishedLabel'),
          value: props.newsItem.date,
        },
      ]
    : [],
)
</script>

<template>
  <FeaturedCard
    :badge="$t('entities.newsItem.featured.badge')"
    :title="newsItem.title"
    :subtitle="newsItem.subtitle"
    :thumbnail-id="newsItem.thumbnailId"
    :to="{ name: 'news-detail', params: { newsItemId: newsItem.id } }"
    :cta-label="$t('entities.newsItem.featured.readMore')"
    :tags="[]"
    :meta="meta"
  />
</template>
