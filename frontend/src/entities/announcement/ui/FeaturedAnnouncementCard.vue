<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { AnnouncementSummary } from '../model/types'
import { FeaturedCard } from '@/shared/ui'

const props = defineProps<{ announcement: AnnouncementSummary }>()

const { t } = useI18n()

const meta = computed(() =>
  props.announcement.date
    ? [
        {
          label: t('entities.announcement.featured.publishedLabel'),
          value: props.announcement.date,
        },
      ]
    : [],
)
</script>

<template>
  <FeaturedCard
    :badge="$t('entities.announcement.featured.badge')"
    :title="announcement.title"
    :subtitle="announcement.subtitle"
    :thumbnail-id="announcement.thumbnailId"
    :to="{ name: 'announcement-detail', params: { announcementId: announcement.id } }"
    :cta-label="$t('entities.announcement.featured.readMore')"
    :tags="[]"
    :meta="meta"
  />
</template>
