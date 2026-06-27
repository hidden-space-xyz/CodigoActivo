<script setup lang="ts">
import AboutPreview from '@/modules/about/presentation/components/AboutPreview.vue'
import { useEventBoard } from '@/modules/events/presentation/composables/useEventBoard'
import { useFeaturedEvent } from '@/modules/events/presentation/composables/useFeaturedEvent'
import EventBoard from '@/modules/events/presentation/components/EventBoard.vue'
import FeaturedEventCard from '@/modules/events/presentation/components/FeaturedEventCard.vue'
import HeroSection from '@/modules/home/presentation/components/HeroSection.vue'
import SponsorsSection from '@/modules/home/presentation/components/SponsorsSection.vue'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const { featuredEvent } = useFeaturedEvent()
const { boardEvents } = useEventBoard()
</script>

<template>
  <div>
    <HeroSection />

    <section v-if="featuredEvent" class="home-featured">
      <div class="ca-container">
        <FeaturedEventCard :event="featuredEvent" />
      </div>
    </section>

    <section class="home-board">
      <div class="ca-container">
        <div class="home-board__head">
          <div>
            <SectionEyebrow text="// próximos eventos" />
            <h2 class="home-board__title">Tablón de eventos</h2>
          </div>
          <BaseButton variant="link" :to="{ name: 'events' }"> Ver todos los eventos → </BaseButton>
        </div>
        <EventBoard :events="boardEvents ?? []" />
      </div>
    </section>

    <AboutPreview />

    <SponsorsSection />
  </div>
</template>

<style scoped>
.home-featured {
  padding: 8px 24px 40px;
}

.home-board {
  padding: 48px 24px 24px;
}

.home-board__head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 30px;
  flex-wrap: wrap;
}

.home-board__title {
  font-family: var(--ca-font-display);
  font-size: 38px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}
</style>
