<script setup lang="ts">
import { computed } from 'vue'

import { NewsCard, useNews } from '@/entities/news-item'
import { AppButton, PageHeading, SearchInput, YearFilter } from '@/shared/ui'

const {
  years,
  selectedYear,
  setYear,
  search,
  setSearch,
  news,
  hasMore,
  loadMore,
  isFetchingMore,
  isLoading,
} = useNews()

const isEmpty = computed(() => !isLoading.value && news.value.length === 0)
</script>

<template>
  <div>
    <section class="news-head">
      <div class="ca-container">
        <PageHeading :title="$t('pages.news.title')" :description="$t('pages.news.intro')" />
      </div>
    </section>

    <section class="news-list-section">
      <div class="ca-container">
        <div v-if="years.length" class="news-filters">
          <YearFilter :years="years" :selected="selectedYear" @select="setYear" />
          <SearchInput
            class="news-filters__search"
            :model-value="search"
            :label="$t('common.searchByTitleOrSubtitle')"
            @update:model-value="setSearch"
          />
        </div>

        <p v-if="isLoading" class="news-loading">{{ $t('common.loading') }}</p>
        <p v-else-if="isEmpty" class="news-loading">
          {{ search ? $t('pages.news.noResults') : $t('pages.news.empty') }}
        </p>
        <div v-else class="news-list">
          <NewsCard v-for="newsItem in news" :key="newsItem.id" :news-item="newsItem" />
        </div>
        <div v-if="hasMore" class="news-more">
          <AppButton
            :label="$t('common.loadMore')"
            plain
            :loading="isFetchingMore"
            @click="loadMore"
          />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.news-head {
  padding: 48px var(--ca-gutter) 20px;
}

.news-list-section {
  padding: 30px var(--ca-gutter) 80px;
}

.news-filters {
  margin-bottom: 26px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 16px 24px;
}

.news-filters__search {
  flex: 0 1 340px;
  min-width: 0;
}

.news-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
  gap: 18px;
}

.news-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.news-more {
  margin-top: 28px;
  display: flex;
  justify-content: center;
}

@media (max-width: 640px) {
  .news-filters__search {
    flex-basis: 100%;
  }
}
</style>
