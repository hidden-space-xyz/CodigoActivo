<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { FeaturedNewsCard, NewsCard, newsQueries } from '@/entities/news-item'

import HomeFeedSection from './HomeFeedSection.vue'

const { data, isLoading } = useQuery(newsQueries.home())
const featured = computed(() => data.value?.featured ?? null)
const recent = computed(() => data.value?.items ?? [])
</script>

<template>
  <HomeFeedSection
    v-if="isLoading || featured || recent.length"
    :title="$t('pages.home.news.title')"
    :view-all-label="$t('pages.home.news.viewAll')"
    :view-all-to="{ name: 'news' }"
    :loading="isLoading"
    compact
  >
    <template #featured>
      <FeaturedNewsCard v-if="featured" :news-item="featured" />
    </template>
    <template v-if="recent.length" #default>
      <NewsCard v-for="newsItem in recent" :key="newsItem.id" :news-item="newsItem" />
    </template>
  </HomeFeedSection>
</template>
