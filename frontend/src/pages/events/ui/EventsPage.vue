<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import {
  PastEventCard,
  usePastEventCategories,
  usePastEventsPaged,
  usePastEventYears,
  useUpcomingEventsPaged,
} from '@/entities/event'
import EventBoard from './EventBoard.vue'
import EventCategoryFilter from './EventCategoryFilter.vue'
import { AppButton, PageHeading, SearchInput, YearFilter } from '@/shared/ui'

const {
  items: upcomingEvents,
  hasMore: hasMoreUpcoming,
  loadMore: loadMoreUpcoming,
  isLoading: isLoadingUpcoming,
  isFetchingMore: isFetchingMoreUpcoming,
} = useUpcomingEventsPaged()

const { years, isLoading: isLoadingYears } = usePastEventYears()
const { categories } = usePastEventCategories()
const selectedYear = ref('')
const search = ref('')
const categoryId = ref('')

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
} = usePastEventsPaged(() => ({
  year: selectedYear.value,
  search: search.value,
  categoryId: categoryId.value,
}))

const isLoadingPast = computed(() => isLoadingYears.value || isLoadingPastEvents.value)
const hasNoPastResults = computed(
  () => selectedYear.value !== '' && !isLoadingPast.value && pastEvents.value.length === 0,
)
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
          <div v-if="years.length" class="events-filters">
            <YearFilter :years="years" :selected="selectedYear" @select="setYear" />
            <div class="events-filters__fields">
              <SearchInput
                v-model="search"
                class="events-filters__search"
                :label="$t('common.searchByTitleOrSubtitle')"
              />
              <EventCategoryFilter
                v-if="categories.length"
                v-model="categoryId"
                class="events-filters__category"
                :categories="categories"
              />
            </div>
          </div>
        </div>
        <p v-if="isLoadingPast" class="events-loading">{{ $t('common.loading') }}</p>
        <p v-else-if="hasNoPastResults" class="events-loading">
          {{ $t('pages.events.noResults') }}
        </p>
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
  padding: 48px var(--ca-gutter) 12px;
}

.events-section {
  padding: 24px var(--ca-gutter);
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

.events-filters {
  margin-top: 20px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 16px 24px;
}

.events-filters__fields {
  display: flex;
  flex: 1 1 420px;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 12px;
  min-width: 0;
  max-width: 600px;
}

.events-filters__search {
  flex: 1 1 240px;
  min-width: 0;
}

.events-filters__category {
  flex: 1 1 200px;
  min-width: 0;
  max-width: 260px;
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

@media (max-width: 640px) {
  .events-filters__fields {
    flex-basis: 100%;
    max-width: none;
  }

  .events-filters__category {
    max-width: none;
  }
}
</style>
