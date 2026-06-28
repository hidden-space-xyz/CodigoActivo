<script setup lang="ts">
import { computed } from 'vue'
import Tag from 'primevue/tag'

// Renders a PrimeVue Tag tinted with a hex color stored in the backend (UserType,
// UserStatusType, AssignmentStatusType). Text color is chosen for contrast, and very
// light tints get a subtle border so they stay visible on light surfaces. Falls back
// to the neutral "secondary" tag when no valid color is provided.
const props = defineProps<{
  value: string
  color?: string | null
}>()

function normalizeHex(input?: string | null): string | null {
  if (!input) return null
  const value = input.trim()
  if (!/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(value)) return null
  if (value.length === 4) {
    return (
      '#' +
      value
        .slice(1)
        .split('')
        .map((ch) => ch + ch)
        .join('')
    )
  }
  return value
}

const tagStyle = computed(() => {
  const hex = normalizeHex(props.color)
  if (!hex) return null
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255
  return {
    backgroundColor: hex,
    color: luminance > 0.6 ? '#1f2937' : '#ffffff',
    border: luminance > 0.85 ? '1px solid rgba(0, 0, 0, 0.15)' : '1px solid transparent',
  }
})
</script>

<template>
  <Tag v-if="tagStyle" :value="value" :style="tagStyle" />
  <Tag v-else :value="value" severity="secondary" />
</template>
