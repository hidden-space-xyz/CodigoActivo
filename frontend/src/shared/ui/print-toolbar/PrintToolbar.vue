<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import type { RouteLocationRaw } from 'vue-router'

import { AppIcon } from '@/shared/ui/app-icon'
import { BrandButton } from '@/shared/ui/brand-button'

defineProps<{
  /** Page the back link returns to. */
  back: RouteLocationRaw
  /** Text of the back link. */
  backLabel: string
  /** Text of the print button. */
  printLabel: string
}>()

const pageStyle = document.createElement('style')
pageStyle.textContent = '@page { size: A4 portrait; margin: 0; }'
onMounted(() => document.head.appendChild(pageStyle))
onBeforeUnmount(() => pageStyle.remove())

function print(): void {
  window.print()
}
</script>

<template>
  <div class="print-toolbar">
    <BrandButton variant="back" :to="back" class="print-toolbar__back">
      {{ backLabel }}
    </BrandButton>
    <button type="button" class="print-toolbar__print" @click="print">
      <AppIcon name="print" />
      <span>{{ printLabel }}</span>
    </button>
  </div>
</template>

<style scoped>
.print-toolbar {
  max-width: 210mm;
  margin: 0 auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.print-toolbar__back {
  margin-bottom: 14px;
}

.print-toolbar__print {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: var(--ca-tap);
  margin-bottom: 14px;
  padding: 0 16px;
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  background: var(--ca-surface);
  color: var(--ca-text);
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
}

.print-toolbar__print:hover {
  border-color: var(--ca-orange);
}

@media screen and (max-width: 1024px) {
  .print-toolbar {
    max-width: none;
  }
}

@media print {
  .print-toolbar {
    display: none !important;
  }
}
</style>
