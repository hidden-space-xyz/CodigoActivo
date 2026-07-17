<script setup lang="ts">
import { computed } from 'vue'

import { AnnouncementCard, useAnnouncements } from '@/entities/announcement'
import { AppButton, SectionEyebrow, YearFilter } from '@/shared/ui'

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
        <SectionEyebrow :text="$t('pages.announcements.eyebrow')" color="var(--ca-orange-ink)" />
        <h1 class="announcements-head__title">{{ $t('pages.announcements.title') }}</h1>
        <p class="announcements-head__intro">
          {{ $t('pages.announcements.intro') }}
        </p>
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
        <p v-else-if="isEmpty" class="announcements-loading">{{ $t('pages.announcements.empty') }}</p>
        <div v-else class="announcements-list">
          <AnnouncementCard
            v-for="announcement in announcements"
            :key="announcement.id"
            :announcement="announcement"
          />
        </div>
        <div v-if="hasMore" class="announcements-more">
          <AppButton :label="$t('common.loadMore')" outlined :loading="isFetchingMore" @click="loadMore" />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.announcements-head {
  padding: 64px 24px 24px;
}

.announcements-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.announcements-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 600px;
}

.announcements-list-section {
  padding: 30px 24px 80px;
}

.announcements-years {
  margin-bottom: 26px;
}

.announcements-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
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
