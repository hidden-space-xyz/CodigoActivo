<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    years: readonly string[]
    selected: string
    /** When true a year is always selected, so the "all" pill is hidden. */
    forced?: boolean
    allValue?: string
    allLabel?: string
  }>(),
  { forced: false, allValue: 'all', allLabel: 'Todos' },
)

const emit = defineEmits<{ select: [year: string] }>()

const options = computed(() => (props.forced ? [...props.years] : [props.allValue, ...props.years]))

function labelFor(year: string): string {
  return year === props.allValue ? props.allLabel : year
}
</script>

<template>
  <div class="year-filter" role="group" aria-label="Filtrar por año">
    <button
      v-for="year in options"
      :key="year"
      type="button"
      class="year-filter__pill"
      :class="{ 'year-filter__pill--active': year === selected }"
      :aria-pressed="year === selected"
      :title="`Filtrar por ${labelFor(year)}`"
      @click="emit('select', year)"
    >
      {{ labelFor(year) }}
    </button>
  </div>
</template>

<style scoped>
.year-filter {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.year-filter__pill {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  background: transparent;
  border: 1px solid var(--ca-border-strong-2);
  padding: 8px 16px;
  border-radius: 999px;
  cursor: pointer;
  transition:
    color 0.15s ease,
    background 0.15s ease,
    border-color 0.15s ease;
}

.year-filter__pill:hover {
  color: #ffffff;
}

.year-filter__pill--active {
  color: var(--ca-bg);
  background: var(--ca-text);
  border-color: var(--ca-text);
}
</style>
