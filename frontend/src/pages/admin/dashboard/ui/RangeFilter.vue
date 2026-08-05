<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AppIcon } from '@/shared/ui'

import { formatDate } from '@/shared/lib'
import { RANGE_OPTIONS, type RangePreset } from '../model/useDashboardRange'

const { t } = useI18n()

const props = defineProps<{
  preset: RangePreset
  customRange: (Date | null)[] | null
}>()

const emit = defineEmits<{
  preset: [RangePreset]
  range: [(Date | null)[] | null]
}>()

const visible = ref(false)
const panelEl = ref<HTMLElement>()
const draft = ref<(Date | null)[] | null>(props.customRange)

const customLabel = computed(() => {
  const range = props.customRange
  if (props.preset !== 'custom' || !(range?.[0] instanceof Date))
    return t('pages.admin.dashboard.range.custom')
  const end = range[1] instanceof Date ? range[1] : range[0]
  return `${formatDate(range[0].toISOString())} – ${formatDate(end.toISOString())}`
})

const pickerValue = computed<Date[]>(() =>
  (draft.value ?? []).filter((date): date is Date => date instanceof Date),
)

function syncDraft(): void {
  draft.value = props.customRange
}

function onSelect(value: unknown): void {
  const range = Array.isArray(value)
    ? value.filter((item): item is Date => item instanceof Date)
    : []
  draft.value = range.length > 0 ? range : null
  if (range.length === 2) {
    emit('range', range)
    visible.value = false
  }
}
</script>

<template>
  <div class="range-filter" role="group" :aria-label="$t('pages.admin.dashboard.range.aria')">
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

    <el-popover
      v-model:visible="visible"
      trigger="click"
      placement="bottom"
      width="auto"
      @before-enter="syncDraft"
    >
      <template #reference>
        <button
          type="button"
          class="range-filter__pill range-filter__pill--custom"
          :class="{ 'range-filter__pill--active': preset === 'custom' }"
          :aria-pressed="preset === 'custom'"
        >
          <AppIcon name="calendar" />
          {{ customLabel }}
        </button>
      </template>

      <div ref="panelEl" class="range-filter__panel" @click.stop @keydown.stop>
        <el-date-picker
          :model-value="pickerValue"
          type="daterange"
          unlink-panels
          :editable="false"
          :start-placeholder="$t('table.rangeFrom')"
          :end-placeholder="$t('table.rangeTo')"
          :append-to="panelEl"
          class="range-filter__picker"
          @update:model-value="onSelect"
        />
      </div>
    </el-popover>
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

.range-filter__pill:hover:not(.range-filter__pill--active) {
  color: var(--ca-text-bright);
  border-color: var(--ca-text-muted);
}

.range-filter__pill--active {
  color: var(--ca-bg);
  background: var(--ca-text);
  border-color: var(--ca-text);
}

.range-filter__pill--active:hover {
  background: var(--ca-text-bright);
  border-color: var(--ca-text-bright);
}

.range-filter__panel {
  padding: 4px;
  min-width: 280px;
}

.range-filter__picker {
  width: 100%;
}
</style>
