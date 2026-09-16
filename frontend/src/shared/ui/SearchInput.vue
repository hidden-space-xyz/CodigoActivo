<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'

import AppIcon from './AppIcon.vue'

const props = withDefaults(
  defineProps<{
    modelValue: string
    label: string
    debounce?: number
  }>(),
  { debounce: 300 },
)

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

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
