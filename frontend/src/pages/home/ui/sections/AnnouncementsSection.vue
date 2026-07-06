<script setup lang="ts">
import {
  AnnouncementCard,
  FeaturedAnnouncementCard,
  useHomeAnnouncements,
} from '@/entities/announcement'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const { featured, items: recent, isLoading } = useHomeAnnouncements()
</script>

<template>
  <section v-if="isLoading || featured" class="home-section">
    <div class="ca-container">
      <div class="home-section__head">
        <div>
          <SectionEyebrow text="// anuncios" color="var(--ca-orange-ink)" />
          <h2 class="home-section__title">Anuncios</h2>
        </div>
        <BaseButton variant="link" :to="{ name: 'announcements' }">
          Ver todas los anuncios →
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
