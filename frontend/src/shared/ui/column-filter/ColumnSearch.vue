<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import type { InputInstance } from 'element-plus'

import ColumnFilterShell from './ColumnFilterShell.vue'

const props = withDefaults(
  defineProps<{
    /** Current search term; `null`, `undefined` or `''` means no filter. */
    modelValue: string | number | null | undefined
    /** Column header text, also used in the toggle's accessible name. */
    label: string
    /** Input placeholder; empty falls back to the localized "search by {label}" text. */
    placeholder?: string
    /** `number` converts the trimmed input with `Number()` before emitting. */
    inputType?: 'text' | 'number'
    /** Delay in milliseconds after the last keystroke before the value is emitted. */
    debounce?: number
  }>(),
  { placeholder: '', inputType: 'text', debounce: 300 },
)

const emit = defineEmits<{
  /**
   * Fired with the trimmed term (`null` when blank) after the debounce, or immediately on Enter or
   * clear. Escape discards the draft without emitting.
   */
  'update:modelValue': [value: string | number | null]
  /** Fired right after each model update so the table can reload its data. */
  apply: []
}>()

const shell = ref<InstanceType<typeof ColumnFilterShell>>()
const input = ref<InputInstance>()
const draft = ref(props.modelValue == null ? '' : String(props.modelValue))
let timer: ReturnType<typeof setTimeout> | undefined

const active = computed(
  () => props.modelValue !== null && props.modelValue !== undefined && props.modelValue !== '',
)

watch(
  () => props.modelValue,
  (value) => {
    const next = value == null ? '' : String(value)
    if (next !== draft.value.trim()) draft.value = next
  },
)

function commit(): void {
  const raw = draft.value.trim()
  const value = raw === '' ? null : props.inputType === 'number' ? Number(raw) : raw
  emit('update:modelValue', value)
  emit('apply')
}

function onInput(): void {
  if (timer) clearTimeout(timer)
  timer = setTimeout(commit, props.debounce)
}

function applyNow(): void {
  if (timer) clearTimeout(timer)
  commit()
  shell.value?.hide()
}

function cancel(): void {
  if (timer) clearTimeout(timer)
  draft.value = props.modelValue == null ? '' : String(props.modelValue)
  shell.value?.hide()
}

function clear(): void {
  draft.value = ''
  applyNow()
}

async function focusInput(): Promise<void> {
  await nextTick()
  input.value?.focus()
}

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
})
</script>

<template>
  <ColumnFilterShell
    ref="shell"
    :label="label"
    :active="active"
    :toggle-label="$t('table.searchBy', { label })"
    :clear-label="$t('table.clearSearch')"
    :show-clear="draft !== ''"
    @show="focusInput"
    @clear="clear"
  >
    <el-input
      ref="input"
      v-model="draft"
      :type="inputType"
      :placeholder="placeholder || $t('table.searchBy', { label })"
      @input="onInput"
      @keydown.enter="applyNow"
      @keydown.esc="cancel"
    />
  </ColumnFilterShell>
</template>
