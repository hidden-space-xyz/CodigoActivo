<script setup lang="ts">
import { BaseChart, ChartCard } from '@/shared/ui/chart'

import type { ChartView } from '../lib/charts'

withDefaults(
  defineProps<{
    /** Card heading. */
    title: string
    /** Short explanation shown under the heading. */
    subtitle: string
    /** Bar chart built by `lib/charts.ts`; its `empty` flag shows the placeholder instead. */
    chart: ChartView
    /** Canvas (or empty placeholder) height in pixels; defaults to 260. */
    height?: number
  }>(),
  { height: 260 },
)
</script>

<template>
  <ChartCard :title="title" :subtitle="subtitle">
    <div v-if="chart.empty" class="analytics-chart__empty" :style="{ height: `${height}px` }">
      {{ $t('pages.admin.dashboard.chart.empty') }}
    </div>
    <div v-else class="analytics-chart__canvas" :style="{ height: `${height}px` }">
      <BaseChart type="bar" :data="chart.data" :options="chart.options" />
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
