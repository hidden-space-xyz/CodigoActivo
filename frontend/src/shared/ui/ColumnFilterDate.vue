<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { useMediaQuery } from '@/shared/lib'

import ColumnFilterShell from './ColumnFilterShell.vue'

const props = defineProps<{
  modelValue: (Date | null)[] | null
  label: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: (Date | null)[] | null]
  apply: []
}>()

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
</script>

<template>
  <ColumnFilterShell
    ref="shell"
    :label="label"
    :active="active"
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
