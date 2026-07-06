<script setup lang="ts">
import type { AnnouncementSummary } from '../model/types'
import { ListThumbnail } from '@/shared/ui'

defineProps<{ announcement: AnnouncementSummary }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'announcement-detail', params: { announcementId: announcement.id } }"
    class="announcement-card"
  >
    <ListThumbnail :thumbnail-id="announcement.thumbnailId" :alt="announcement.title" />

    <time v-if="announcement.date" class="announcement-card__date">{{ announcement.date }}</time>
    <h3 class="announcement-card__title">{{ announcement.title }}</h3>
    <p v-if="announcement.subtitle" class="announcement-card__subtitle">
      {{ announcement.subtitle }}
    </p>
  </RouterLink>
</template>

<style scoped>
.announcement-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 16px;
  text-decoration: none;
  color: inherit;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.announcement-card:hover {
  transform: translateY(-4px);
  border-color: var(--ca-border-strong);
}

.announcement-card__date {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
  margin-top: 4px;
}

.announcement-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 18px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.announcement-card__subtitle {
  font-size: 14.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}
</style>
