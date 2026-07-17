<script setup lang="ts">
import { computed, ref } from 'vue'
import DatePicker from 'primevue/datepicker'
import Popover from 'primevue/popover'

import { formatDate } from '@/shared/lib'
import { RANGE_OPTIONS, type RangePreset } from '../model/useDashboardRange'

const props = defineProps<{
  preset: RangePreset
  customRange: (Date | null)[] | null
}>()

const emit = defineEmits<{
  preset: [RangePreset]
  range: [(Date | null)[] | null]
}>()

const panel = ref<InstanceType<typeof Popover>>()
const draft = ref<(Date | null)[] | null>(props.customRange)

const customLabel = computed(() => {
  const range = props.customRange
  if (props.preset !== 'custom' || !(range?.[0] instanceof Date)) return 'Personalizado'
  const end = range[1] instanceof Date ? range[1] : range[0]
  return `${formatDate(range[0].toISOString())} – ${formatDate(end.toISOString())}`
})

function toggle(event: MouseEvent): void {
  draft.value = props.customRange
  panel.value?.toggle(event)
}

function onSelect(): void {
  const range = draft.value
  if (range?.[0] instanceof Date && range[1] instanceof Date) {
    emit('range', range)
    panel.value?.hide()
  }
}
</script>

<template>
  <div class="range-filter" role="group" aria-label="Rango de tiempo">
    <button
      v-for="option in RANGE_OPTIONS"
      :key="option.value"
      type="button"
      class="range-filter__pill"
      :class="{ 'range-filter__pill--active': preset === option.value }"
      :aria-pressed="preset === option.value"
      @click="emit('preset', option.value)"
    >
      {{ option.label }}
    </button>

    <button
      type="button"
      class="range-filter__pill range-filter__pill--custom"
      :class="{ 'range-filter__pill--active': preset === 'custom' }"
      :aria-pressed="preset === 'custom'"
      @click="toggle"
    >
      <i class="pi pi-calendar" aria-hidden="true" />
      {{ customLabel }}
    </button>

    <Popover ref="panel">
      <div class="range-filter__panel" @click.stop>
        <DatePicker
          v-model="draft"
          selection-mode="range"
          :manual-input="false"
          date-format="dd/mm/yy"
          placeholder="Desde – Hasta"
          inline
          @update:model-value="onSelect"
        />
      </div>
    </Popover>
  </div>
</template>

<style scoped>
.range-filter {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
}

.range-filter__pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  font-weight: 600;
  color: var(--ca-text-muted);
  background: transparent;
  border: 1px solid var(--ca-border-strong-2);
  padding: 7px 14px;
  border-radius: 999px;
  cursor: pointer;
  transition:
    color 0.15s ease,
    background 0.15s ease,
    border-color 0.15s ease;
}

.range-filter__pill:hover {
  color: var(--ca-text-bright);
}

.range-filter__pill--active {
  color: var(--ca-bg);
  background: var(--ca-text);
  border-color: var(--ca-text);
}

.range-filter__panel {
  padding: 4px;
}
</style>
