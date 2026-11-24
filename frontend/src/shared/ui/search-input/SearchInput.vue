<script setup lang="ts">
import { ref, watch } from 'vue'
import { useTimeoutFn } from '@vueuse/core'

import { MAX_SEARCH_LENGTH } from '@/shared/config'
import { AppIcon } from '@/shared/ui/app-icon'

const props = withDefaults(
  defineProps<{
    /** Applied search term; a change from outside replaces the text in the box. */
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
const { start, stop } = useTimeoutFn(commit, () => props.debounce, { immediate: false })

watch(
  () => props.modelValue,
  (value) => {
    if (value !== draft.value.trim()) draft.value = value
  },
)

function commit(): void {
  stop()
  const value = draft.value.trim()
  if (value !== props.modelValue) emit('update:modelValue', value)
}

function onInput(): void {
  start()
}
</script>

<template>
  <el-input
    v-model="draft"
    class="search-input"
    clearable
    enterkeyhint="search"
    :maxlength="MAX_SEARCH_LENGTH"
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
