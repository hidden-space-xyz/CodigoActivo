<script setup lang="ts">
import { EventCategoriesSection } from '@/features/manage-catalogs'
import { AdminPageHeader } from '@/shared/ui'
</script>

<template>
  <div>
    <AdminPageHeader title="Configuración" subtitle="Catálogos configurables del sistema" />

    <div class="catalogs-grid">
      <EventCategoriesSection />
    </div>
  </div>
</template>

<style scoped>
.catalogs-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(420px, 1fr));
  gap: 18px;
}
</style>
