<script setup lang="ts">
import { useHomeEvents } from '@/modules/events/presentation/composables/useHomeEvents'
import EventCard from '@/modules/events/presentation/components/EventCard.vue'
import FeaturedEventCard from '@/modules/events/presentation/components/FeaturedEventCard.vue'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const { featured, items, isLoading } = useHomeEvents()
</script>

<template>
  <section v-if="isLoading || featured" class="home-section">
    <div class="ca-container">
      <div class="home-section__head">
        <div>
          <SectionEyebrow text="// eventos" color="var(--ca-cyan)" />
          <h2 class="home-section__title">Eventos</h2>
        </div>
        <BaseButton variant="link" :to="{ name: 'events' }"> Ver todos los eventos → </BaseButton>
      </div>

      <p v-if="isLoading" class="home-section__loading">Cargando…</p>
      <template v-else-if="featured">
        <FeaturedEventCard :event="featured" />
        <div v-if="items.length" class="home-section__grid">
          <EventCard v-for="event in items" :key="event.id" :event="event" />
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
