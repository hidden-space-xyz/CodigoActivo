<script setup lang="ts">
import { useSeo } from '@/shared/lib'

import { organizationJsonLd } from '../model/organizationJsonLd'
import AboutPreview from './sections/AboutPreview.vue'
import AnnouncementsSection from './sections/AnnouncementsSection.vue'
import HomeEventsSection from './sections/HomeEventsSection.vue'
import HeroSection from './sections/HeroSection.vue'
import SponsorsSection from './sections/SponsorsSection.vue'

useSeo(() => ({ jsonLd: organizationJsonLd() }))
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
