<script setup lang="ts">
import { computed } from 'vue'

import { AnnouncementCard, useAnnouncements } from '@/entities/announcement'
import { AppButton, PageHeading, SearchInput, YearFilter } from '@/shared/ui'

const {
  years,
  selectedYear,
  setYear,
  search,
  setSearch,
  announcements,
  hasMore,
  loadMore,
  isFetchingMore,
  isLoading,
} = useAnnouncements()

const isEmpty = computed(() => !isLoading.value && announcements.value.length === 0)
</script>

<template>
  <div>
    <section class="announcements-head">
      <div class="ca-container">
        <PageHeading
          :title="$t('pages.announcements.title')"
          :description="$t('pages.announcements.intro')"
        />
      </div>
    </section>

    <section class="announcements-list-section">
      <div class="ca-container">
        <div v-if="years.length" class="announcements-filters">
          <YearFilter :years="years" :selected="selectedYear" @select="setYear" />
          <SearchInput
            class="announcements-filters__search"
            :model-value="search"
            :label="$t('common.searchByTitleOrSubtitle')"
            @update:model-value="setSearch"
          />
        </div>

        <p v-if="isLoading" class="announcements-loading">{{ $t('common.loading') }}</p>
        <p v-else-if="isEmpty" class="announcements-loading">
          {{ search ? $t('pages.announcements.noResults') : $t('pages.announcements.empty') }}
        </p>
        <div v-else class="announcements-list">
          <AnnouncementCard
            v-for="announcement in announcements"
            :key="announcement.id"
            :announcement="announcement"
          />
        </div>
        <div v-if="hasMore" class="announcements-more">
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
.announcements-head {
  padding: 48px var(--ca-gutter) 20px;
}

.announcements-list-section {
  padding: 30px var(--ca-gutter) 80px;
}

.announcements-filters {
  margin-bottom: 26px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 16px 24px;
}

.announcements-filters__search {
  flex: 0 1 340px;
  min-width: 0;
}

.announcements-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
  gap: 18px;
}

.announcements-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.announcements-more {
  margin-top: 28px;
  display: flex;
  justify-content: center;
}

@media (max-width: 640px) {
  .announcements-filters__search {
    flex-basis: 100%;
  }
}
</style>
