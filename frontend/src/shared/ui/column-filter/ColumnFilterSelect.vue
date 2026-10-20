<script setup lang="ts">
import { computed, ref } from 'vue'

import ColumnFilterShell from './ColumnFilterShell.vue'
import { useTableFilterRegistration } from './filter-registry'

const props = withDefaults(
  defineProps<{
    /** Selected option value; `null` means no filter. */
    modelValue: string | boolean | null
    /** Column header text, also used in the toggle's accessible name and the placeholder. */
    label: string
    /** Choices listed in the popover; boolean values support yes/no columns. */
    options: { label: string; value: string | boolean }[]
    /** `header` sits next to a column title; `toolbar` is a labelled pill above the table. */
    variant?: 'header' | 'toolbar'
    /** Lets the user type to narrow a long list of options. */
    filterable?: boolean
  }>(),
  { variant: 'header', filterable: false },
)

const emit = defineEmits<{
  /** Fired when an option is picked or cleared (`null`); the popover then closes. */
  'update:modelValue': [value: string | boolean | null]
  /** Fired right after each model update so the table can reload its data. */
  apply: []
}>()

const shell = ref<InstanceType<typeof ColumnFilterShell>>()

const active = computed(() => props.modelValue !== null && props.modelValue !== undefined)

function commit(value: string | boolean | null): void {
  emit('update:modelValue', value)
  emit('apply')
}

function onChange(value: unknown): void {
  commit(value === undefined || value === '' ? null : (value as string | boolean))
  shell.value?.hide()
}

function clear(): void {
  commit(null)
  shell.value?.hide()
}

useTableFilterRegistration(() => {
  if (!active.value) return null
  const selected = props.options.find((option) => option.value === props.modelValue)
  return { label: props.label, value: selected?.label ?? String(props.modelValue) }
}, clear)
</script>

<template>
  <ColumnFilterShell
    ref="shell"
    :label="label"
    :active="active"
    icon="filter"
    :variant="variant"
    :toggle-label="$t('table.filterBy', { label })"
    :clear-label="$t('table.clearFilter')"
    :show-clear="active"
    @clear="clear"
  >
    <template #default="{ panel }">
      <el-select
        :model-value="modelValue"
        :placeholder="$t('table.filterBy', { label })"
        clearable
        :filterable="filterable"
        :append-to="panel"
        class="column-filter-select"
        @change="onChange"
      >
        <el-option
          v-for="option in options"
          :key="String(option.value)"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
    </template>
  </ColumnFilterShell>
</template>

<style scoped>
.column-filter-select {
  width: 100%;
}
</style>
