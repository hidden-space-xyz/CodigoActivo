<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import {
  PastEventCard,
  usePastEventsPaged,
  usePastEventYears,
  useUpcomingEventsPaged,
} from '@/entities/event'
import EventBoard from './EventBoard.vue'
import { AppButton, PageHeading, YearFilter } from '@/shared/ui'

const {
  items: upcomingEvents,
  hasMore: hasMoreUpcoming,
  loadMore: loadMoreUpcoming,
  isLoading: isLoadingUpcoming,
  isFetchingMore: isFetchingMoreUpcoming,
} = useUpcomingEventsPaged()

const { years, isLoading: isLoadingYears } = usePastEventYears()
const selectedYear = ref('')

watch(
  years,
  (list) => {
    const [first] = list
    if (first && !selectedYear.value) selectedYear.value = first
  },
  { immediate: true },
)

function setYear(year: string): void {
  selectedYear.value = year
}

const {
  items: pastEvents,
  hasMore: hasMorePast,
  loadMore: loadMorePast,
  isLoading: isLoadingPastEvents,
  isFetchingMore: isFetchingMorePast,
} = usePastEventsPaged(() => selectedYear.value)

const isLoadingPast = computed(() => isLoadingYears.value || isLoadingPastEvents.value)
</script>

<template>
  <div>
    <section class="events-head">
      <div class="ca-container">
        <PageHeading :title="$t('pages.events.title')" :description="$t('pages.events.intro')" />
      </div>
    </section>

    <section class="events-section">
      <div class="ca-container">
        <div class="events-section__head">
          <h2 class="events-section__title">{{ $t('pages.events.upcomingTitle') }}</h2>
        </div>
        <p v-if="isLoadingUpcoming" class="events-loading">{{ $t('common.loading') }}</p>
        <EventBoard v-else :events="upcomingEvents" />
        <div v-if="hasMoreUpcoming" class="events-more">
          <AppButton
            :label="$t('common.loadMore')"
            plain
            :loading="isFetchingMoreUpcoming"
            @click="loadMoreUpcoming"
          />
        </div>
      </div>
    </section>

    <section class="events-section events-section--past">
      <div class="ca-container">
        <div class="events-section__head">
          <h2 class="events-section__title">{{ $t('pages.events.pastTitle') }}</h2>
          <YearFilter
            class="events-section__filter"
            :years="years"
            :selected="selectedYear"
            @select="setYear"
          />
        </div>
        <p v-if="isLoadingPast" class="events-loading">{{ $t('common.loading') }}</p>
        <div v-else class="events-grid">
          <PastEventCard v-for="event in pastEvents" :key="event.id" :event="event" />
        </div>
        <div v-if="hasMorePast" class="events-more">
          <AppButton
            :label="$t('common.loadMore')"
            plain
            :loading="isFetchingMorePast"
            @click="loadMorePast"
          />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.events-head {
  padding: 64px var(--ca-gutter) 16px;
}

.events-section {
  padding: 32px var(--ca-gutter);
}

.events-section--past {
  padding-bottom: 80px;
}

.events-section__head {
  margin-bottom: 24px;
}

.events-section__title {
  font-family: var(--ca-font-display);
  font-size: 32px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.events-section__filter {
  margin-top: 20px;
}

.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(290px, 100%), 1fr));
  gap: 22px;
}

.events-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.events-more {
  margin-top: 28px;
  display: flex;
  justify-content: center;
}
</style>
