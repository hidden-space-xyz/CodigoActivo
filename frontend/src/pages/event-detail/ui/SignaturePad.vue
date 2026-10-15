<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import DrawingPad from 'signature_pad'

import { ActionButton } from '@/shared/ui/action-button'

const MAX_PIXEL_RATIO = 2

defineProps<{
  /** Accessible name of the drawing area, naming the document being signed. */
  label: string
  /** Shows the drawing area with the invalid border. */
  invalid?: boolean
}>()

/** PNG data URL of the drawing, or an empty string while nothing is drawn. */
const signature = defineModel<string>({ required: true })

const canvas = ref<HTMLCanvasElement | null>(null)
let pad: DrawingPad | null = null
let observer: ResizeObserver | null = null

function inkColor(element: HTMLElement): string {
  return getComputedStyle(element).getPropertyValue('--ca-signature-ink').trim() || 'black'
}

function fitCanvas(): void {
  const element = canvas.value
  if (!element || !pad) return
  const ratio = Math.min(Math.max(window.devicePixelRatio || 1, 1), MAX_PIXEL_RATIO)
  const strokes = pad.toData()
  element.width = element.offsetWidth * ratio
  element.height = element.offsetHeight * ratio
  element.getContext('2d')?.scale(ratio, ratio)
  pad.clear()
  pad.fromData(strokes)
}

function save(): void {
  signature.value = pad && !pad.isEmpty() ? pad.toDataURL('image/png') : ''
}

function clear(): void {
  pad?.clear()
  signature.value = ''
}

watch(signature, (value) => {
  if (!value && pad && !pad.isEmpty()) pad.clear()
})

onMounted(() => {
  const element = canvas.value
  if (!element) return
  pad = new DrawingPad(element, { penColor: inkColor(element) })
  pad.addEventListener('endStroke', save)
  fitCanvas()
  if (typeof ResizeObserver === 'undefined') {
    window.addEventListener('resize', fitCanvas)
  } else {
    observer = new ResizeObserver(fitCanvas)
    observer.observe(element)
  }
})

onBeforeUnmount(() => {
  observer?.disconnect()
  window.removeEventListener('resize', fitCanvas)
  pad?.off()
  pad = null
})
</script>

<template>
  <div class="signature-pad">
    <canvas
      ref="canvas"
      class="signature-pad__canvas"
      :class="{ 'signature-pad__canvas--invalid': invalid }"
      role="img"
      :aria-label="label"
    />
    <div class="signature-pad__footer">
      <span class="signature-pad__hint">{{ $t('pages.eventDetail.terms.signature.hint') }}</span>
      <ActionButton
        :label="$t('pages.eventDetail.terms.signature.clear')"
        icon="undo"
        text
        size="small"
        :disabled="!signature"
        @click="clear"
      />
    </div>
  </div>
</template>

<style scoped>
.signature-pad {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.signature-pad__canvas {
  display: block;
  width: 100%;
  height: 160px;
  background: var(--ca-signature-paper);
  border: 1px dashed var(--ca-border-strong);
  border-radius: 10px;
  touch-action: none;
  cursor: crosshair;
}

.signature-pad__canvas--invalid {
  border: 1px solid var(--ca-danger);
}

.signature-pad__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.signature-pad__hint {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}
</style>
