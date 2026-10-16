<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import ColumnFilterShell from './ColumnFilterShell.vue'
import { useTableFilterRegistration } from './filter-registry'
import { formatDate, formatDateRange, toDateOnly } from '@/shared/lib/date'
import { useMediaQuery } from '@/shared/lib/media-query'

const props = withDefaults(
  defineProps<{
    /** Selected `[from, to]` range; `null` means no filter. */
    modelValue: (Date | null)[] | null
    /** Column header text, also used in the toggle's accessible name. */
    label: string
    /** `header` sits next to a column title; `toolbar` is a labelled pill above the table. */
    variant?: 'header' | 'toolbar'
  }>(),
  { variant: 'header' },
)

const emit = defineEmits<{
  /**
   * Fired on every pick, including a half-selected range, and with `null` once no date remains;
   * the popover closes when both ends are chosen.
   */
  'update:modelValue': [value: (Date | null)[] | null]
  /** Fired right after each model update so the table can reload its data. */
  apply: []
}>()

const { t } = useI18n()
const shell = ref<InstanceType<typeof ColumnFilterShell>>()
const draft = ref<(Date | null)[] | null>(props.modelValue)
const narrow = useMediaQuery('(max-width: 640px)')

const active = computed(() => (props.modelValue ?? []).some((date) => date instanceof Date))

const pickerValue = computed<Date[]>(() =>
  (draft.value ?? []).filter((date): date is Date => date instanceof Date),
)

watch(
  () => props.modelValue,
  (value) => {
    draft.value = value ?? null
  },
)

function commit(): void {
  const range = draft.value ?? []
  emit('update:modelValue', range.some((date) => date instanceof Date) ? draft.value : null)
  emit('apply')
}

function onSelect(value: unknown): void {
  const range = Array.isArray(value)
    ? value.filter((item): item is Date => item instanceof Date)
    : []
  draft.value = range.length > 0 ? range : null
  commit()
  if (range.length === 2) shell.value?.hide()
}

function clear(): void {
  draft.value = null
  commit()
  shell.value?.hide()
}

function describeRange(): string | null {
  const [start, end] = props.modelValue ?? []
  const from = start instanceof Date ? toDateOnly(start) : null
  const to = end instanceof Date ? toDateOnly(end) : null
  if (from && to) return formatDateRange(from, to)
  if (from) return t('table.rangeSince', { date: formatDate(from) })
  if (to) return t('table.rangeUntil', { date: formatDate(to) })
  return null
}

useTableFilterRegistration(() => {
  const value = describeRange()
  return value === null ? null : { label: props.label, value }
}, clear)
</script>

<template>
  <ColumnFilterShell
    ref="shell"
    :label="label"
    :active="active"
    icon="calendar"
    :variant="variant"
    :toggle-label="$t('table.filterBy', { label })"
    :clear-label="$t('table.clearFilter')"
    :show-clear="active || draft != null"
    wide
    @clear="clear"
  >
    <template #default="{ panel }">
      <el-date-picker
        :model-value="pickerValue"
        type="daterange"
        :single-panel="narrow"
        :unlink-panels="!narrow"
        :editable="narrow"
        :start-placeholder="$t('table.rangeFrom')"
        :end-placeholder="$t('table.rangeTo')"
        :append-to="panel"
        class="column-filter-date"
        @update:model-value="onSelect"
      />
    </template>
  </ColumnFilterShell>
</template>

<style scoped>
.column-filter-date {
  width: 100%;
}
</style>
