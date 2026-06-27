<script setup lang="ts">
import { computed } from 'vue'

import { useAnnouncements } from '@/modules/announcements/presentation/composables/useAnnouncements'
import AnnouncementCard from '@/modules/announcements/presentation/components/AnnouncementCard.vue'
import FeaturedAnnouncementCard from '@/modules/announcements/presentation/components/FeaturedAnnouncementCard.vue'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const { announcements, isLoading } = useAnnouncements()

// Highlighted announcement: the admin-selected one, else the most recent (the
// list comes sorted by date descending), then the next 3 excluding it.
const featured = computed(() => {
  const list = announcements.value ?? []
  return list.find((announcement) => announcement.featured) ?? list[0] ?? null
})
const recent = computed(() =>
  (announcements.value ?? []).filter((a) => a.id !== featured.value?.id).slice(0, 3),
)
</script>

<template>
  <section v-if="isLoading || featured" class="home-section">
    <div class="ca-container">
      <div class="home-section__head">
        <div>
          <SectionEyebrow text="// anuncios" color="var(--ca-amber)" />
          <h2 class="home-section__title">Anuncios</h2>
        </div>
        <BaseButton variant="link" :to="{ name: 'announcements' }">
          Ver todas las noticias →
        </BaseButton>
      </div>

      <p v-if="isLoading" class="home-section__loading">Cargando…</p>
      <template v-else-if="featured">
        <FeaturedAnnouncementCard :announcement="featured" />
        <div v-if="recent.length" class="home-section__grid">
          <AnnouncementCard
            v-for="announcement in recent"
            :key="announcement.id"
            :announcement="announcement"
          />
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.home-section {
  padding: 40px 24px 8px;
}

.home-section__head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 26px;
  flex-wrap: wrap;
}

.home-section__title {
  font-family: var(--ca-font-display);
  font-size: 38px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.home-section__grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18px;
  margin-top: 18px;
}

.home-section__loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

@media (max-width: 860px) {
  .home-section__grid {
    grid-template-columns: 1fr;
  }
}
</style>
