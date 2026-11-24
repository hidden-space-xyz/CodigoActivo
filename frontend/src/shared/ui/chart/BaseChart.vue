<script setup lang="ts">
import { Chart as ChartJS, registerables, type ChartData, type ChartOptions } from 'chart.js'
import { Chart } from 'vue-chartjs'

ChartJS.register(...registerables)

defineProps<{
  /** Chart.js chart type. Changing it destroys and recreates the chart. */
  type: 'line' | 'bar' | 'doughnut'
  /** Chart.js `data`; watched deeply, and a change updates the drawn chart in place. */
  data: object
  /** Chart.js `options`; a new object updates the drawn chart in place. */
  options: object
}>()
</script>

<template>
  <Chart
    :key="type"
    class="base-chart"
    :type="type"
    :data="data as ChartData"
    :options="options as ChartOptions"
  />
</template>

<style scoped>
.base-chart {
  display: block;
}
</style>
