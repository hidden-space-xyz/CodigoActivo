<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import DatePicker from 'primevue/datepicker'

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

const active = computed(() => (props.modelValue ?? []).some((date) => date instanceof Date))

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

function onSelect(): void {
  commit()
  if (draft.value?.[0] instanceof Date && draft.value[1] instanceof Date) shell.value?.hide()
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
    <DatePicker
      v-model="draft"
      selection-mode="range"
      :manual-input="false"
      :placeholder="$t('table.rangePlaceholder')"
      show-icon
      fluid
      @update:model-value="onSelect"
    />
  </ColumnFilterShell>
</template>
