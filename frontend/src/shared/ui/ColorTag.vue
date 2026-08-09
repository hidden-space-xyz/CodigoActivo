<script setup lang="ts">
import { computed } from 'vue'

import { hexLuminance, normalizeHexColor } from '@/shared/lib'

const props = defineProps<{
  value: string
  color?: string | null
}>()

const tagStyle = computed(() => {
  const hex = normalizeHexColor(props.color)
  if (!hex) return null
  const luminance = hexLuminance(hex)
  return {
    '--el-tag-bg-color': hex,
    '--el-tag-text-color': luminance > 0.6 ? '#1f2937' : '#ffffff',
    '--el-tag-border-color': luminance > 0.85 ? 'rgba(0, 0, 0, 0.15)' : 'transparent',
  }
})
</script>

<template>
  <el-tag v-if="tagStyle" class="color-tag" :style="tagStyle">{{ value }}</el-tag>
  <el-tag v-else class="color-tag" type="info">{{ value }}</el-tag>
</template>

<style scoped>
.color-tag {
  max-width: 100%;
  height: auto;
  min-height: 24px;
  white-space: normal;
  overflow-wrap: anywhere;
}
</style>
