<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import InputText from 'primevue/inputtext'

import ColumnFilterShell from './ColumnFilterShell.vue'

const props = withDefaults(
  defineProps<{
    modelValue: string | number | null | undefined
    label: string
    placeholder?: string
    inputType?: 'text' | 'number'
    debounce?: number
  }>(),
  { placeholder: '', inputType: 'text', debounce: 300 },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | number | null]
  apply: []
}>()

const shell = ref<InstanceType<typeof ColumnFilterShell>>()
const input = ref<InstanceType<typeof InputText>>()
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
  const el = (input.value as unknown as { $el?: HTMLElement } | undefined)?.$el
  el?.focus()
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
    <InputText
      ref="input"
      v-model="draft"
      :type="inputType"
      :placeholder="placeholder || $t('table.searchBy', { label })"
      fluid
      @input="onInput"
      @keydown.enter="applyNow"
      @keydown.esc="cancel"
    />
  </ColumnFilterShell>
</template>
