<script setup lang="ts">
import ContentEntityPage from '@/features/content/ContentEntityPage.vue'
import { useResourcesAdmin } from '@/features/resources/useResourcesAdmin'

const controller = useResourcesAdmin()
</script>

<template>
  <ContentEntityPage
    title="Recursos"
    subtitle="Material formativo publicado"
    new-label="Nuevo recurso"
    entity-label="recurso"
    :controller="controller"
  />
</template>
