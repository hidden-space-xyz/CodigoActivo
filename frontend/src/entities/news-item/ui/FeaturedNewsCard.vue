<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { formatDate } from '@/shared/lib/date'
import { FeaturedCard } from '@/shared/ui/featured-card'

import type { NewsSummary } from '../model/types'

const props = defineProps<{
  /** News item shown as the large home highlight with its publication date. */
  newsItem: NewsSummary
}>()

const { t } = useI18n()

const meta = computed(() => [
  {
    label: t('entities.newsItem.featured.publishedLabel'),
    value: formatDate(props.newsItem.createdAt),
  },
])
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
