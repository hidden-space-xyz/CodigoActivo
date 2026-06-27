<script setup lang="ts">
import AboutPreview from '@/modules/about/presentation/components/AboutPreview.vue'
import AnnouncementsSection from '@/modules/announcements/presentation/components/AnnouncementsSection.vue'
import { useFeaturedEvent } from '@/modules/events/presentation/composables/useFeaturedEvent'
import FeaturedEventCard from '@/modules/events/presentation/components/FeaturedEventCard.vue'
import HeroSection from '@/modules/home/presentation/components/HeroSection.vue'
import SponsorsSection from '@/modules/home/presentation/components/SponsorsSection.vue'

const { featuredEvent } = useFeaturedEvent()
</script>

<template>
  <div>
    <HeroSection />

    <AnnouncementsSection />

    <section v-if="featuredEvent" class="home-featured">
      <div class="ca-container">
        <FeaturedEventCard :event="featuredEvent" />
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
</style>
