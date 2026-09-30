<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { formatDateRange } from '@/shared/lib/date'
import { FeaturedCard } from '@/shared/ui/featured-card'

import { statusLabelKey } from '../model/status'
import type { EventSummary } from '../model/types'

const props = defineProps<{
  /** Event highlighted on the home page; its subtitle is shown quoted. */
  event: EventSummary
}>()

const { t } = useI18n()

const meta = computed(() => [
  {
    label: t('entities.event.featured.dateLabel'),
    value: formatDateRange(props.event.startsAt, props.event.endsAt),
  },
  { label: t('common.status'), value: t(statusLabelKey(props.event.status)) },
])
</script>

<template>
  <FeaturedCard
    :badge="$t('entities.event.featured.badge')"
    :title="event.title"
    :subtitle="`«${event.subtitle}»`"
    :thumbnail-id="event.thumbnailId"
    :to="{ name: 'event-detail', params: { eventId: event.id } }"
    :cta-label="$t('entities.event.featured.viewDetails')"
    :tags="event.categories"
    :meta="meta"
  />
</template>
