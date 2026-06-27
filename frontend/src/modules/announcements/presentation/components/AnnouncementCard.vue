<script setup lang="ts">
import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'

defineProps<{ announcement: Announcement }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'announcement-detail', params: { announcementId: announcement.id } }"
    class="announcement-card"
  >
    <span class="announcement-card__icon" aria-hidden="true">📣</span>
    <div class="announcement-card__body">
      <h3 class="announcement-card__title">{{ announcement.title }}</h3>
      <p class="announcement-card__subtitle">{{ announcement.subtitle }}</p>
    </div>
    <time v-if="announcement.date" class="announcement-card__date">{{ announcement.date }}</time>
  </RouterLink>
</template>

<style scoped>
.announcement-card {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 20px 22px;
  text-decoration: none;
  color: inherit;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.announcement-card:hover {
  transform: translateY(-3px);
  border-color: rgba(255, 255, 255, 0.2);
}

.announcement-card__icon {
  flex-shrink: 0;
  width: 42px;
  height: 42px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  font-size: 18px;
  background: rgba(45, 212, 217, 0.14);
}

.announcement-card__body {
  flex: 1;
  min-width: 0;
}

.announcement-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 18px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.announcement-card__subtitle {
  margin-top: 5px;
  font-size: 14.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}

.announcement-card__date {
  flex-shrink: 0;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
  white-space: nowrap;
}
</style>
