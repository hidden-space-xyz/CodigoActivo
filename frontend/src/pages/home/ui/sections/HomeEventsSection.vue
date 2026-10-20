<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { EventCard, eventQueries, FeaturedEventCard } from '@/entities/event'

import HomeFeedSection from './HomeFeedSection.vue'

const { data, isLoading } = useQuery(eventQueries.home())
const featured = computed(() => data.value?.featured ?? null)
const items = computed(() => data.value?.items ?? [])
</script>

<template>
  <HomeFeedSection
    v-if="isLoading || featured || items.length"
    :title="$t('pages.home.events.title')"
    :view-all-label="$t('pages.home.events.viewAll')"
    :view-all-to="{ name: 'events' }"
    :loading="isLoading"
  >
    <template #featured>
      <FeaturedEventCard v-if="featured" :event="featured" />
    </template>
    <template v-if="items.length" #default>
      <EventCard v-for="event in items" :key="event.id" :event="event" />
    </template>
  </HomeFeedSection>
</template>
