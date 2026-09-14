<script setup lang="ts">
import { computed } from 'vue'

import { AnnouncementCard, useAnnouncements } from '@/entities/announcement'
import { AppButton, PageHeading, YearFilter } from '@/shared/ui'

const {
  years,
  selectedYear,
  setYear,
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
        <YearFilter
          v-if="years.length"
          class="announcements-years"
          :years="years"
          :selected="selectedYear"
          @select="setYear"
        />

        <p v-if="isLoading" class="announcements-loading">{{ $t('common.loading') }}</p>
        <p v-else-if="isEmpty" class="announcements-loading">
          {{ $t('pages.announcements.empty') }}
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

.announcements-years {
  margin-bottom: 26px;
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
</style>
