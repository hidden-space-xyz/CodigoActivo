<script setup lang="ts">
import { AdminPageHeader } from '@/shared/ui/admin-page-header'

import EventCategoriesSection from './EventCategoriesSection.vue'
import TermsDocumentsSection from './TermsDocumentsSection.vue'
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.catalogs.header.title')"
      :subtitle="$t('pages.admin.catalogs.header.subtitle')"
    />

    <div class="catalogs-grid">
      <EventCategoriesSection />
      <TermsDocumentsSection />
    </div>
  </div>
</template>

<style scoped>
.catalogs-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(420px, 100%), 1fr));
  gap: 18px;
}
</style>
