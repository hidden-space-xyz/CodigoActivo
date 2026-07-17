<script setup lang="ts">
defineProps<{
  years: readonly string[]
  selected: string
}>()

const emit = defineEmits<{ select: [year: string] }>()
</script>

<template>
  <div class="year-filter" role="group" :aria-label="$t('table.filterByYear')">
    <button
      v-for="year in years"
      :key="year"
      type="button"
      class="year-filter__pill"
      :class="{ 'year-filter__pill--active': year === selected }"
      :aria-pressed="year === selected"
      :title="$t('table.filterByYearValue', { year })"
      @click="emit('select', year)"
    >
      {{ year }}
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
  color: var(--ca-text-bright);
}

.year-filter__pill--active {
  color: var(--ca-bg);
  background: var(--ca-text);
  border-color: var(--ca-text);
}
</style>
