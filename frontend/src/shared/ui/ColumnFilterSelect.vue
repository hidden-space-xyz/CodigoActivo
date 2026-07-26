<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Select from 'primevue/select'

import ColumnFilterShell from './ColumnFilterShell.vue'

const props = defineProps<{
  modelValue: string | boolean | null
  label: string
  options: { label: string; value: string | boolean }[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | boolean | null]
  apply: []
}>()

const shell = ref<InstanceType<typeof ColumnFilterShell>>()
const draft = ref<string | boolean | null>(props.modelValue)

const active = computed(() => props.modelValue !== null && props.modelValue !== undefined)

watch(
  () => props.modelValue,
  (value) => {
    draft.value = value ?? null
  },
)

function commit(): void {
  emit('update:modelValue', draft.value)
  emit('apply')
}

function onChange(): void {
  commit()
  shell.value?.hide()
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
    :show-clear="draft != null"
    @clear="clear"
  >
    <Select
      v-model="draft"
      :options="options"
      option-label="label"
      option-value="value"
      :placeholder="$t('table.filterBy', { label })"
      show-clear
      fluid
      @change="onChange"
    />
  </ColumnFilterShell>
</template>
