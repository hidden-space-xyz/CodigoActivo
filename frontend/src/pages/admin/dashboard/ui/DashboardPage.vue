<script setup lang="ts">
import { computed } from 'vue'

import { AdminPageHeader, DataState } from '@/shared/ui'
import { useChartTheme } from '@/shared/lib'

import { useDashboardRange } from '../model/useDashboardRange'
import { useDashboardAnalytics } from '../model/useDashboardAnalytics'
import * as charts from '../model/charts'
import KpiCards from './KpiCards.vue'
import RangeFilter from './RangeFilter.vue'
import AnalyticsChart from './AnalyticsChart.vue'
import OccupancyCard from './OccupancyCard.vue'

const { preset, customRange, range, setPreset, setCustomRange } = useDashboardRange()
const { data, isLoading, isError, isFetching } = useDashboardAnalytics(range)
const { palette } = useChartTheme()

const granularity = computed(() => data.value?.granularity ?? 'month')

const chartOptions = computed(() => {
  const p = palette.value
  return {
    userGrowth: charts.areaOptions(p),
    inscriptions: charts.barOptions(p, true),
    content: charts.barOptions(p, false),
    calendar: charts.barOptions(p, false),
    usersByType: charts.doughnutOptions(p),
    audience: charts.doughnutOptions(p),
    resources: charts.doughnutOptions(p),
    categories: charts.doughnutOptions(p),
  }
})

const view = computed(() => {
  const p = palette.value
  const g = granularity.value
  const d = data.value
  const o = chartOptions.value
  const topEvents = d?.topEvents ?? []
  return {
    userGrowth: {
      data: charts.stackedAreaData(d?.userGrowth, g, p, charts.USER_TYPE_STYLE),
      options: o.userGrowth,
      empty: !charts.hasSeriesData(d?.userGrowth),
    },
    inscriptions: {
      data: charts.barSeriesData(d?.inscriptions, g, p, charts.INSCRIPTION_STATUS_STYLE),
      options: o.inscriptions,
      empty: !charts.hasSeriesData(d?.inscriptions),
    },
    content: {
      data: charts.barSeriesData(d?.contentPublished, g, p, charts.CONTENT_STYLE),
      options: o.content,
      empty: !charts.hasSeriesData(d?.contentPublished),
    },
    calendar: {
      data: charts.barSeriesData(d?.eventsCalendar, 'month', p, charts.CALENDAR_STYLE),
      options: o.calendar,
      empty: !charts.hasSeriesData(d?.eventsCalendar),
    },
    usersByType: {
      data: charts.doughnutData(d?.usersByType, p, charts.USER_TYPE_STYLE),
      options: o.usersByType,
      empty: !charts.hasSliceData(d?.usersByType),
    },
    audience: {
      data: charts.doughnutData(d?.audienceComposition, p, charts.AUDIENCE_STYLE),
      options: o.audience,
      empty: !charts.hasSliceData(d?.audienceComposition),
    },
    resources: {
      data: charts.doughnutData(d?.resourcesByType, p, charts.RESOURCE_TYPE_STYLE),
      options: o.resources,
      empty: !charts.hasSliceData(d?.resourcesByType),
    },
    categories: {
      data: charts.doughnutData(d?.eventsByCategory, p),
      options: o.categories,
      empty: !charts.hasSliceData(d?.eventsByCategory),
    },
    topEvents: {
      data: charts.rankingBarData(
        topEvents.map((event) => charts.wrapLabel(event.title ?? '')),
        topEvents.map((event) => event.confirmed ?? 0),
        p.orange,
        p,
      ),
      options: charts.rankingOptions(
        p,
        topEvents.map((event) => event.title ?? ''),
      ),
      empty: topEvents.length === 0,
    },
  }
})
</script>

<template>
  <div>
    <AdminPageHeader title="Panel" subtitle="Crecimiento y actividad de la plataforma">
      <template #actions>
        <RangeFilter
          :preset="preset"
          :custom-range="customRange"
          @preset="setPreset"
          @range="setCustomRange"
        />
      </template>
    </AdminPageHeader>

    <DataState
      :loading="isLoading"
      :error="isError"
      :empty="false"
      error-text="No se pudieron cargar las estadísticas."
    >
      <div v-if="data" class="dashboard" :class="{ 'dashboard--refetching': isFetching }">
        <KpiCards :kpis="data.kpis ?? []" />

        <div class="dashboard__grid">
          <div class="g-7">
            <AnalyticsChart
              title="Crecimiento de usuarios"
              subtitle="Total acumulado por tipo"
              type="line"
              :data="view.userGrowth.data"
              :options="view.userGrowth.options"
              :height="280"
              :empty="view.userGrowth.empty"
            />
          </div>
          <div class="g-5">
            <AnalyticsChart
              title="Inscripciones a lo largo del tiempo"
              subtitle="Altas por estado en el periodo"
              type="bar"
              :data="view.inscriptions.data"
              :options="view.inscriptions.options"
              :height="280"
              :empty="view.inscriptions.empty"
            />
          </div>

          <div class="g-7">
            <AnalyticsChart
              title="Eventos con más inscripciones"
              subtitle="Confirmadas · histórico"
              type="bar"
              :data="view.topEvents.data"
              :options="view.topEvents.options"
              :height="340"
              :empty="view.topEvents.empty"
            />
          </div>
          <div class="g-5">
            <AnalyticsChart
              title="Publicación de contenido"
              subtitle="Anuncios y recursos publicados en el periodo"
              type="bar"
              :data="view.content.data"
              :options="view.content.options"
              :height="340"
              :empty="view.content.empty"
            />
          </div>

          <div class="g-4">
            <AnalyticsChart
              title="Usuarios por tipo"
              subtitle="Composición actual"
              type="doughnut"
              :data="view.usersByType.data"
              :options="view.usersByType.options"
              :height="240"
              :empty="view.usersByType.empty"
            />
          </div>
          <div class="g-4">
            <AnalyticsChart
              title="Adultos y menores"
              subtitle="Composición actual"
              type="doughnut"
              :data="view.audience.data"
              :options="view.audience.options"
              :height="240"
              :empty="view.audience.empty"
            />
          </div>
          <div class="g-4">
            <AnalyticsChart
              title="Recursos por tipo"
              subtitle="Composición actual"
              type="doughnut"
              :data="view.resources.data"
              :options="view.resources.options"
              :height="240"
              :empty="view.resources.empty"
            />
          </div>

          <div class="g-5">
            <AnalyticsChart
              title="Eventos por categoría"
              subtitle="Composición actual"
              type="doughnut"
              :data="view.categories.data"
              :options="view.categories.options"
              :height="300"
              :empty="view.categories.empty"
            />
          </div>
          <div class="g-7">
            <AnalyticsChart
              title="Calendario de eventos"
              subtitle="Eventos por mes · pasados y próximos (±6 meses)"
              type="bar"
              :data="view.calendar.data"
              :options="view.calendar.options"
              :height="300"
              :empty="view.calendar.empty"
            />
          </div>

          <div class="g-12">
            <OccupancyCard :occupancy="data.occupancy ?? {}" />
          </div>
        </div>
      </div>
    </DataState>
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 20px;
  transition: opacity 0.2s ease;
}

.dashboard--refetching {
  opacity: 0.72;
}

.dashboard__grid {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: 18px;
}

.g-4 {
  grid-column: span 4;
}

.g-5 {
  grid-column: span 5;
}

.g-7 {
  grid-column: span 7;
}

.g-12 {
  grid-column: span 12;
}

@media (max-width: 1100px) {
  .g-4 {
    grid-column: span 6;
  }

  .g-5,
  .g-7 {
    grid-column: span 12;
  }
}

@media (max-width: 680px) {
  .g-4 {
    grid-column: span 12;
  }
}
</style>
