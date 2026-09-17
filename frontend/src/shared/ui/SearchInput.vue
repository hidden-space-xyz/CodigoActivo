<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'

import AppIcon from './AppIcon.vue'

const props = withDefaults(
  defineProps<{
    /** Current search term. */
    modelValue: string
    /** Used as both placeholder and accessible name, since there is no visible label. */
    label: string
    /** Delay in milliseconds after the last keystroke before the term is emitted. */
    debounce?: number
  }>(),
  { debounce: 300 },
)

const emit = defineEmits<{
  /**
   * Fired with the trimmed term after the debounce, or immediately on Enter or clear; skipped when
   * it equals the current value.
   */
  'update:modelValue': [value: string]
}>()

const draft = ref(props.modelValue)
let timer: ReturnType<typeof setTimeout> | undefined

watch(
  () => props.modelValue,
  (value) => {
    if (value !== draft.value.trim()) draft.value = value
  },
)

function commit(): void {
  if (timer) clearTimeout(timer)
  const value = draft.value.trim()
  if (value !== props.modelValue) emit('update:modelValue', value)
}

function onInput(): void {
  if (timer) clearTimeout(timer)
  timer = setTimeout(commit, props.debounce)
}

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
})
</script>

<template>
  <el-input
    v-model="draft"
    class="search-input"
    clearable
    enterkeyhint="search"
    :placeholder="label"
    :aria-label="label"
    @input="onInput"
    @clear="commit"
    @keydown.enter="commit"
  >
    <template #prefix>
      <AppIcon name="search" />
    </template>
  </el-input>
</template>
