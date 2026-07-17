<script setup lang="ts">
import Chart from 'primevue/chart'

import ChartCard from './ChartCard.vue'

withDefaults(
  defineProps<{
    title: string
    subtitle: string
    type: 'line' | 'bar' | 'doughnut'
    data: object
    options: object
    height?: number
    empty?: boolean
  }>(),
  { height: 260, empty: false },
)
</script>

<template>
  <ChartCard :title="title" :subtitle="subtitle">
    <div v-if="empty" class="analytics-chart__empty" :style="{ height: `${height}px` }">
      Sin datos en este periodo.
    </div>
    <div v-else class="analytics-chart__canvas" :style="{ height: `${height}px` }">
      <Chart :type="type" :data="data" :options="options" />
    </div>
  </ChartCard>
</template>

<style scoped>
.analytics-chart__canvas {
  position: relative;
}

.analytics-chart__canvas :deep(.p-chart) {
  height: 100%;
}

.analytics-chart__canvas :deep(canvas) {
  height: 100% !important;
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
