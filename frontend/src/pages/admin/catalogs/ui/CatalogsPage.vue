<script setup lang="ts">
import { CatalogSection, useActivityRoleTypes } from '@/features/manage-catalogs'
import { AdminPageHeader } from '@/shared/ui'

const activityRoleTypes = useActivityRoleTypes()
</script>

<template>
  <div>
    <AdminPageHeader
      title="Configuración"
      subtitle="Roles de actividad configurables del sistema"
    />

    <div class="catalogs-grid">
      <CatalogSection title="Roles de actividad" :controller="activityRoleTypes" />
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
