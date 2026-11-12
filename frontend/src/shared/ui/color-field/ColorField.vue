<script setup lang="ts">
import { computed } from 'vue'

import { DEFAULT_TAG_COLOR, toHexColor } from '@/shared/lib/color'
import { ColorTag } from '@/shared/ui/color-tag'

/** Chosen color, with or without its leading `#`; clearing the picker restores the default one. */
const color = defineModel<string>({ required: true })

defineProps<{
  /** Id of the label, which names the group of controls. */
  labelId: string
  /** Visible name of the field. */
  label: string
  /** Text of the tag that previews the color. */
  preview: string
}>()

const hex = computed(() => toHexColor(color.value))

function pick(value: string | null): void {
  color.value = value ?? DEFAULT_TAG_COLOR
}
</script>

<template>
  <div class="ca-field">
    <div :id="labelId" class="ca-field__label">{{ label }}</div>
    <div class="color-field" role="group" :aria-labelledby="labelId">
      <el-color-picker :model-value="hex" color-format="hex" @update:model-value="pick" />
      <ColorTag :value="preview" :color="hex" />
      <span class="color-field__hex">{{ hex }}</span>
    </div>
  </div>
</template>

<style scoped>
.color-field {
  display: flex;
  align-items: center;
  gap: 12px;
}

.color-field__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}

@media (max-width: 640px) {
  .color-field {
    flex-wrap: wrap;
  }
}
</style>
