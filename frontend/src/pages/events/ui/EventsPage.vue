<script setup lang="ts">
import { PastEventCard, usePastEvents, useUpcomingEvents } from '@/entities/event'
import EventBoard from './EventBoard.vue'
import { SectionEyebrow, YearFilter } from '@/shared/ui'

const { upcomingEvents, isLoading: isLoadingUpcoming } = useUpcomingEvents()
const { pastEvents, years, selectedYear, setYear, isLoading: isLoadingPast } = usePastEvents()
</script>

<template>
  <div>
    <section class="events-head">
      <div class="ca-container">
        <SectionEyebrow text="// eventos" color="var(--ca-cyan)" />
        <h1 class="events-head__title">Eventos</h1>
        <p class="events-head__intro">
          Todo lo que tenemos por delante y todo lo que ya hemos hecho juntos. Explora los próximos
          eventos y el archivo.
        </p>
      </div>
    </section>

    <section class="events-section">
      <div class="ca-container">
        <div class="events-section__head">
          <SectionEyebrow text="// próximamente" color="var(--ca-green)" />
          <h2 class="events-section__title">Próximos eventos</h2>
        </div>
        <p v-if="isLoadingUpcoming" class="events-loading">Cargando…</p>
        <EventBoard v-else :events="upcomingEvents ?? []" />
      </div>
    </section>

    <section class="events-section events-section--past">
      <div class="ca-container">
        <div class="events-section__head">
          <SectionEyebrow text="// archivo" color="var(--ca-amber)" />
          <h2 class="events-section__title">Eventos anteriores</h2>
          <YearFilter
            class="events-section__filter"
            :years="years"
            :selected="selectedYear"
            @select="setYear"
          />
        </div>
        <p v-if="isLoadingPast" class="events-loading">Cargando…</p>
        <div v-else class="events-grid">
          <PastEventCard v-for="event in pastEvents" :key="event.id" :event="event" />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.events-head {
  padding: 64px 24px 16px;
}

.events-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.events-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 560px;
}

.events-section {
  padding: 32px 24px;
}

.events-section--past {
  padding-bottom: 80px;
}

.events-section__head {
  margin-bottom: 24px;
}

.events-section__title {
  font-family: var(--ca-font-display);
  font-size: 32px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.events-section__filter {
  margin-top: 20px;
}

.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
  gap: 22px;
}

.events-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}
</style>
