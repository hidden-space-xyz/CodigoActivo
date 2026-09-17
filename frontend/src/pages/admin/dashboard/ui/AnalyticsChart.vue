<script setup lang="ts">
import BaseChart from './BaseChart.vue'
import ChartCard from './ChartCard.vue'

withDefaults(
  defineProps<{
    /** Card heading. */
    title: string
    /** Short explanation shown under the heading. */
    subtitle: string
    /** Chart.js chart type; changing it recreates the chart. */
    type: 'line' | 'bar' | 'doughnut'
    /** Chart.js `data` object, typically built by the helpers in `model/charts.ts`. */
    data: object
    /** Chart.js `options` object matching `type`. */
    options: object
    /** Canvas (or empty placeholder) height in pixels; defaults to 260. */
    height?: number
    /** Shows the "no data" placeholder instead of rendering the chart. */
    empty?: boolean
  }>(),
  { height: 260, empty: false },
)
</script>

<template>
  <ChartCard :title="title" :subtitle="subtitle">
    <div v-if="empty" class="analytics-chart__empty" :style="{ height: `${height}px` }">
      {{ $t('pages.admin.dashboard.chart.empty') }}
    </div>
    <div v-else class="analytics-chart__canvas" :style="{ height: `${height}px` }">
      <BaseChart :type="type" :data="data" :options="options" />
    </div>
  </ChartCard>
</template>

<style scoped>
.analytics-chart__canvas {
  position: relative;
}

.analytics-chart__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--ca-text-dim);
  font-size: 13.5px;
  border: 1px dashed var(--ca-border-strong);
  border-radius: 12px;
}
</style>
