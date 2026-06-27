<script setup lang="ts">
import ContentEntityPage from '@/features/content/ContentEntityPage.vue'
import { useAnnouncements } from '@/features/announcements/useAnnouncements'

const controller = useAnnouncements()
</script>

<template>
  <ContentEntityPage
    title="Anuncios"
    subtitle="Comunicaciones para la comunidad"
    new-label="Nuevo anuncio"
    entity-label="anuncio"
    :controller="controller"
  />
</template>
