<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { useChartTheme } from '@/shared/lib/chart'
import { formatNumber, formatScore } from '@/shared/lib/number'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { DataState } from '@/shared/ui/data-state'

import { dashboardQueries } from '../api/queries'
import * as charts from '../lib/charts'
import { useDashboardRange } from '../model/use-dashboard-range'
import AnalyticsChart from './AnalyticsChart.vue'
import KpiCards from './KpiCards.vue'
import OccupancyCard from './OccupancyCard.vue'
import PendingSignupsCard from './PendingSignupsCard.vue'
import RangeFilter from './RangeFilter.vue'
import RoleCoverageCard from './RoleCoverageCard.vue'

const { t } = useI18n()
const { preset, customRange, range, setPreset, setCustomRange } = useDashboardRange()
const { data, isLoading, isError, isFetching } = useQuery(() =>
  dashboardQueries.analytics(range.value),
)
const { palette } = useChartTheme()

const view = computed(() => {
  const d = data.value
  if (!d) return null
  const p = palette.value
  return {
    newUsers: charts.timeSeriesChart(d.newUsers, d.granularity, p, charts.USER_TYPE_STYLE, t),
    inscriptions: charts.timeSeriesChart(
      d.inscriptions,
      d.granularity,
      p,
      charts.INSCRIPTION_STATUS_STYLE,
      t,
    ),
    topEvents: charts.topEventsChart(d.topEvents, p, t),
    satisfaction: charts.satisfactionChart(d.satisfaction, p, t),
    categoryDemand: charts.categoryDemandChart(d.categoryDemand, p, t),
    participantProfile: charts.participantProfileChart(d.participantProfile, p, t),
    recurrence: charts.recurrenceChart(d.recurrence, p, t),
  }
})

const satisfactionSubtitle = computed(() => {
  const satisfaction = data.value?.satisfaction
  if (satisfaction?.average == null) return t('pages.admin.dashboard.charts.satisfaction.subtitle')
  return t(
    'pages.admin.dashboard.charts.satisfaction.summary',
    { average: formatScore(satisfaction.average), n: formatNumber(satisfaction.scored) },
    satisfaction.scored,
  )
})
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.dashboard.header.title')"
      :subtitle="$t('pages.admin.dashboard.header.subtitle')"
    >
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
      :error-text="$t('pages.admin.dashboard.error')"
    >
      <div v-if="data && view" class="dashboard" :class="{ 'dashboard--refetching': isFetching }">
        <KpiCards :kpis="data.kpis" />

        <section class="dashboard__section" aria-labelledby="dashboard-upcoming">
          <header>
            <h2 id="dashboard-upcoming" class="dashboard__section-title">
              {{ $t('pages.admin.dashboard.sections.upcoming.title') }}
            </h2>
            <p class="dashboard__section-subtitle">
              {{ $t('pages.admin.dashboard.sections.upcoming.subtitle') }}
            </p>
          </header>
          <div class="dashboard__grid">
            <div class="g-7">
              <PendingSignupsCard :pending="data.pendingSignups" />
            </div>
            <div class="g-5">
              <RoleCoverageCard :roles="data.roleCoverage" />
            </div>
            <div class="g-12">
              <OccupancyCard :occupancy="data.occupancy" />
            </div>
          </div>
        </section>

        <section class="dashboard__section" aria-labelledby="dashboard-period">
          <header>
            <h2 id="dashboard-period" class="dashboard__section-title">
              {{ $t('pages.admin.dashboard.sections.period.title') }}
            </h2>
            <p class="dashboard__section-subtitle">
              {{ $t('pages.admin.dashboard.sections.period.subtitle') }}
            </p>
          </header>
          <div class="dashboard__grid">
            <div class="g-7">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.newUsers.title')"
                :subtitle="$t('pages.admin.dashboard.charts.newUsers.subtitle')"
                :chart="view.newUsers"
                :height="280"
              />
            </div>
            <div class="g-5">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.inscriptions.title')"
                :subtitle="$t('pages.admin.dashboard.charts.inscriptions.subtitle')"
                :chart="view.inscriptions"
                :height="280"
              />
            </div>

            <div class="g-7">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.topEvents.title')"
                :subtitle="$t('pages.admin.dashboard.charts.topEvents.subtitle')"
                :chart="view.topEvents"
                :height="380"
              />
            </div>
            <div class="g-5">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.satisfaction.title')"
                :subtitle="satisfactionSubtitle"
                :chart="view.satisfaction"
                :height="380"
              />
            </div>

            <div class="g-4">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.categoryDemand.title')"
                :subtitle="$t('pages.admin.dashboard.charts.categoryDemand.subtitle')"
                :chart="view.categoryDemand"
                :height="300"
              />
            </div>
            <div class="g-4">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.participantProfile.title')"
                :subtitle="$t('pages.admin.dashboard.charts.participantProfile.subtitle')"
                :chart="view.participantProfile"
                :height="300"
              />
            </div>
            <div class="g-4">
              <AnalyticsChart
                :title="$t('pages.admin.dashboard.charts.recurrence.title')"
                :subtitle="$t('pages.admin.dashboard.charts.recurrence.subtitle')"
                :chart="view.recurrence"
                :height="300"
              />
            </div>
          </div>
        </section>
      </div>
    </DataState>
  </div>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 28px;
  transition: opacity 0.2s ease;
}

.dashboard--refetching {
  opacity: 0.72;
}

.dashboard__section {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.dashboard__section-title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
  letter-spacing: -0.01em;
}

.dashboard__section-subtitle {
  margin-top: 2px;
  font-size: 13px;
  color: var(--ca-text-muted);
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

@media (max-width: 1024px) {
  .g-4 {
    grid-column: span 6;
  }

  .g-5,
  .g-7 {
    grid-column: span 12;
  }
}

@media (max-width: 640px) {
  .g-4 {
    grid-column: span 12;
  }
}
</style>
