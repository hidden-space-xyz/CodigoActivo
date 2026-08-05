<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Chart, registerables, type ChartConfiguration } from 'chart.js'

Chart.register(...registerables)

const props = defineProps<{
  type: 'line' | 'bar' | 'doughnut'
  data: object
  options: object
}>()

const canvas = ref<HTMLCanvasElement | null>(null)
let chart: Chart | null = null

function destroyChart(): void {
  chart?.destroy()
  chart = null
}

function createChart(): void {
  const element = canvas.value
  if (!element) return
  chart = new Chart(element, {
    type: props.type,
    data: props.data,
    options: props.options,
  } as ChartConfiguration)
}

function reinit(): void {
  destroyChart()
  createChart()
}

onMounted(createChart)
onBeforeUnmount(destroyChart)

watch(() => props.type, reinit)
watch(() => props.data, reinit, { deep: true })
watch(() => props.options, reinit, { deep: true })
</script>

<template>
  <canvas ref="canvas" class="base-chart" />
</template>

<style scoped>
.base-chart {
  display: block;
}
</style>
