<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { UpcomingEvent } from '../model/types'
import { FeaturedCard } from '@/shared/ui'

const props = defineProps<{ event: UpcomingEvent }>()

const { t } = useI18n()

const meta = computed(() => [
  { label: t('entities.event.featured.dateLabel'), value: props.event.date },
  { label: t('common.status'), value: props.event.status.label },
])
</script>

<template>
  <FeaturedCard
    :badge="$t('entities.event.featured.badge')"
    :title="event.title"
    :subtitle="`«${event.slogan}»`"
    :thumbnail-id="event.thumbnailId"
    :to="{ name: 'event-detail', params: { eventId: event.id } }"
    :cta-label="$t('entities.event.featured.viewDetails')"
    :tags="event.categories"
    :meta="meta"
  />
</template>
