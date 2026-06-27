<script setup lang="ts">
import AboutPreview from '@/modules/about/presentation/components/AboutPreview.vue'
import AnnouncementsSection from '@/modules/announcements/presentation/components/AnnouncementsSection.vue'
import HomeEventsSection from '@/modules/events/presentation/components/HomeEventsSection.vue'
import HeroSection from '@/modules/home/presentation/components/HeroSection.vue'
import SponsorsSection from '@/modules/home/presentation/components/SponsorsSection.vue'
</script>

<template>
  <div>
    <HeroSection />

    <AnnouncementsSection />

    <HomeEventsSection />

    <AboutPreview />

    <SponsorsSection />
  </div>
</template>
