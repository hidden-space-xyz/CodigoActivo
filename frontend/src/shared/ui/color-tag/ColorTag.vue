<script setup lang="ts">
import { computed } from 'vue'
import { hexLuminance, normalizeHexColor } from '@/shared/lib/color'

const props = withDefaults(
  defineProps<{
    /** Tag text, typically a catalog item name. */
    value: string
    /**
     * Hex background (`#rgb` or `#rrggbb`); text contrast is picked from its luminance. Missing or
     * invalid values fall back to the neutral `info` tag, or a neutral dot.
     */
    color?: string | null
    /** `solid` is a filled tag; `dot` a coloured dot before plain text, for dense tables. */
    variant?: 'solid' | 'dot'
    /** Keeps a solid tag on one line with an ellipsis and its full text as tooltip. */
    truncate?: boolean
  }>(),
  { color: null, variant: 'solid', truncate: false },
)

const hex = computed(() => normalizeHexColor(props.color))

const tagStyle = computed(() => {
  if (!hex.value) return null
  const luminance = hexLuminance(hex.value)
  return {
    '--el-tag-bg-color': hex.value,
    '--el-tag-text-color': luminance > 0.6 ? 'var(--ca-paper-text)' : 'var(--ca-paper)',
    '--el-tag-border-color': luminance > 0.85 ? 'var(--ca-paper-edge)' : 'transparent',
  }
})

const dotStyle = computed(() => (hex.value ? { background: hex.value } : undefined))

const tagTitle = computed(() => (props.truncate ? props.value : undefined))
</script>

<template>
  <span v-if="variant === 'dot'" class="color-tag color-tag--dot" :title="value">
    <span class="color-tag__dot" aria-hidden="true" :style="dotStyle" />
    <span class="color-tag__text">{{ value }}</span>
  </span>
  <el-tag
    v-else-if="tagStyle"
    class="color-tag"
    :class="{ 'color-tag--truncate': truncate }"
    :title="tagTitle"
    :style="tagStyle"
    disable-transitions
    >{{ value }}</el-tag
  >
  <el-tag
    v-else
    class="color-tag"
    :class="{ 'color-tag--truncate': truncate }"
    :title="tagTitle"
    type="info"
    disable-transitions
    >{{ value }}</el-tag
  >
</template>

<style scoped>
.color-tag {
  max-width: 100%;
  height: auto;
  min-height: 24px;
  white-space: normal;
  overflow-wrap: anywhere;
}

.color-tag--truncate {
  min-width: 0;
  height: 24px;
  min-height: 0;
  white-space: nowrap;
}

.color-tag--truncate :deep(.el-tag__content) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.color-tag--dot {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
  max-width: 100%;
  min-height: 0;
  color: var(--ca-text);
  white-space: nowrap;
  vertical-align: middle;
}

.color-tag__dot {
  flex: none;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ca-text-faint);
  box-shadow: 0 0 0 1px var(--ca-border);
}

.color-tag__text {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
