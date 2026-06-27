<script setup lang="ts">
import { computed } from 'vue'

import { useAnnouncements } from '@/modules/announcements/presentation/composables/useAnnouncements'
import AnnouncementCard from '@/modules/announcements/presentation/components/AnnouncementCard.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const { announcements, isLoading } = useAnnouncements()

const hasAnnouncements = computed(() => (announcements.value?.length ?? 0) > 0)
</script>

<template>
  <section v-if="isLoading || hasAnnouncements" class="announcements">
    <div class="ca-container">
      <div class="announcements__head">
        <SectionEyebrow text="// anuncios" color="var(--ca-amber)" />
        <h2 class="announcements__title">Anuncios</h2>
      </div>

      <p v-if="isLoading" class="announcements__loading">Cargando…</p>
      <div v-else class="announcements__list">
        <AnnouncementCard
          v-for="announcement in announcements"
          :key="announcement.id"
          :announcement="announcement"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.announcements {
  padding: 32px 24px;
}

.announcements__head {
  margin-bottom: 24px;
}

.announcements__title {
  font-family: var(--ca-font-display);
  font-size: 32px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.announcements__list {
  display: grid;
  gap: 14px;
}

.announcements__loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}
</style>
