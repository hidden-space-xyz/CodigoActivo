<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { useSeo } from '@/shared/lib/seo'

import { organizationJsonLd } from '../lib/organization-json-ld'
import HeroSection from './sections/HeroSection.vue'
import HomeEventsSection from './sections/HomeEventsSection.vue'
import NewsSection from './sections/NewsSection.vue'
import SponsorsSection from './sections/SponsorsSection.vue'

const { t } = useI18n()

useSeo(() => ({ jsonLd: organizationJsonLd(t) }))
</script>

<template>
  <div>
    <HeroSection />

    <NewsSection />

    <HomeEventsSection />

    <SponsorsSection />
  </div>
</template>
