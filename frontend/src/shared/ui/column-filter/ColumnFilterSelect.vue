<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import ColumnFilterShell from './ColumnFilterShell.vue'

const props = defineProps<{
  /** Selected option value; `null` means no filter. */
  modelValue: string | boolean | null
  /** Column header text, also used in the toggle's accessible name and the placeholder. */
  label: string
  /** Choices listed in the popover; boolean values support yes/no columns. */
  options: { label: string; value: string | boolean }[]
}>()

const emit = defineEmits<{
  /** Fired when an option is picked or cleared (`null`); the popover then closes. */
  'update:modelValue': [value: string | boolean | null]
  /** Fired right after each model update so the table can reload its data. */
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

function onChange(value: unknown): void {
  draft.value = value === undefined || value === '' ? null : (value as string | boolean)
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
    <template #default="{ panel }">
      <el-select
        :model-value="draft"
        :placeholder="$t('table.filterBy', { label })"
        clearable
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
