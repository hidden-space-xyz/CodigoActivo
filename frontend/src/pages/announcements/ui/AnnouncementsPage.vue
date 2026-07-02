<script setup lang="ts">
import { computed } from 'vue'

import { AnnouncementCard, useAnnouncements } from '@/entities/announcement'
import { SectionEyebrow, YearFilter } from '@/shared/ui'

const { years, selectedYear, setYear, announcements, isLoading } = useAnnouncements()

const isEmpty = computed(() => !isLoading.value && (announcements.value?.length ?? 0) === 0)
</script>

<template>
  <div>
    <section class="announcements-head">
      <div class="ca-container">
        <SectionEyebrow text="// anuncios" color="var(--ca-amber)" />
        <h1 class="announcements-head__title">Anuncios</h1>
        <p class="announcements-head__intro">
          Novedades y comunicaciones de la comunidad de Código Activo.
        </p>
      </div>
    </section>

    <section class="announcements-list-section">
      <div class="ca-container">
        <YearFilter
          v-if="years.length"
          class="announcements-years"
          :years="years"
          :selected="selectedYear"
          forced
          @select="setYear"
        />

        <p v-if="isLoading" class="announcements-loading">Cargando…</p>
        <p v-else-if="isEmpty" class="announcements-loading">Todavía no hay anuncios.</p>
        <div v-else class="announcements-list">
          <AnnouncementCard
            v-for="announcement in announcements"
            :key="announcement.id"
            :announcement="announcement"
          />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.announcements-head {
  padding: 64px 24px 24px;
}

.announcements-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.announcements-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 600px;
}

.announcements-list-section {
  padding: 30px 24px 80px;
}

.announcements-years {
  margin-bottom: 26px;
}

.announcements-list {
  display: grid;
  gap: 14px;
}

.announcements-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}
</style>
