<script setup lang="ts">
import { computed, h, useAttrs, type Component } from 'vue'

import AppIcon from './AppIcon.vue'

defineOptions({ inheritAttrs: false })

const props = defineProps<{ tooltip?: string }>()

const attrs = useAttrs()

const label = computed(() => (typeof attrs.label === 'string' ? attrs.label : ''))

const iconName = computed(() => (typeof attrs.icon === 'string' ? attrs.icon.trim() : ''))

const buttonIcon = computed<Component | string>(() => {
  const name = iconName.value
  if (name === '') return ''
  return () => h(AppIcon, { name })
})

const buttonAttrs = computed<Record<string, unknown>>(() => {
  const rest: Record<string, unknown> = { ...attrs }
  delete rest.label
  delete rest.icon
  return rest
})

const tooltipText = computed(
  () => props.tooltip ?? (attrs['aria-label'] as string | undefined) ?? label.value,
)
</script>

<template>
  <el-tooltip placement="top" :content="tooltipText" :disabled="!tooltipText">
    <el-button v-if="$slots.default || label" v-bind="buttonAttrs" :icon="buttonIcon">
      <slot>{{ label }}</slot>
    </el-button>
    <el-button v-else v-bind="buttonAttrs" :icon="buttonIcon" />
  </el-tooltip>
</template>
