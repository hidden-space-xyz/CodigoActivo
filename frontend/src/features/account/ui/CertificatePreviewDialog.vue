<script setup lang="ts">
import { ref, watch } from 'vue'

import type { AccountCertificate } from '@/entities/account'
import { AppIcon, BaseButton } from '@/shared/ui'

import { SHEET_RATIO, renderCertificatePreview } from '../model/certificate-sheet'
import type { CertificateFormat } from '../model/useAccountCertificates'

const props = defineProps<{
  certificate: AccountCertificate | null
  busyPng: boolean
  busyPdf: boolean
}>()

const emit = defineEmits<{
  download: [AccountCertificate, CertificateFormat]
  close: []
}>()

const canvasEl = ref<HTMLCanvasElement | null>(null)
const painting = ref(false)
const failed = ref(false)

let renderToken = 0

watch(
  () => [props.certificate, canvasEl.value] as const,
  async ([certificate, canvas]) => {
    if (!certificate || !canvas) return
    const token = ++renderToken
    painting.value = true
    failed.value = false
    try {
      await renderCertificatePreview(canvas, certificate)
    } catch {
      if (token === renderToken) failed.value = true
    } finally {
      if (token === renderToken) painting.value = false
    }
  },
  { immediate: true, flush: 'post' },
)
</script>

<template>
  <el-dialog
    :model-value="certificate !== null"
    :title="$t('features.account.certificates.dialog.header')"
    width="min(94vw, 1080px)"
    align-center
    append-to-body
    @update:model-value="(value: boolean) => !value && emit('close')"
  >
    <div v-if="certificate" class="cert-preview">
      <div class="cert-preview__stage" :style="{ aspectRatio: String(SHEET_RATIO) }">
        <canvas
          ref="canvasEl"
          class="cert-preview__canvas"
          role="img"
          :aria-label="certificate.eventTitle"
        />
        <div v-if="painting" class="cert-preview__veil">{{ $t('common.loading') }}</div>
        <div v-else-if="failed" class="cert-preview__veil">
          {{ $t('features.account.certificates.renderError') }}
        </div>
      </div>

      <p class="cert-preview__hint">{{ $t('features.account.certificates.dialog.hint') }}</p>
    </div>

    <template #footer>
      <div class="cert-preview__actions">
        <BaseButton variant="ghost" @click="emit('close')">
          {{ $t('common.close') }}
        </BaseButton>
        <BaseButton
          variant="ghost"
          :loading="busyPdf"
          @click="certificate && emit('download', certificate, 'pdf')"
        >
          <AppIcon v-if="!busyPdf" name="download" />
          <span>{{ $t('features.account.certificates.downloadPdf') }}</span>
        </BaseButton>
        <BaseButton :loading="busyPng" @click="certificate && emit('download', certificate, 'png')">
          <AppIcon v-if="!busyPng" name="download" />
          <span>{{ $t('features.account.certificates.download') }}</span>
        </BaseButton>
      </div>
    </template>
  </el-dialog>
</template>

<style scoped>
.cert-preview {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.cert-preview__stage {
  position: relative;
  width: 100%;
  border-radius: 10px;
  overflow: hidden;
  background: #fdfbf7;
  box-shadow: var(--ca-shadow-md);
}

.cert-preview__canvas {
  display: block;
  width: 100%;
  height: 100%;
}

.cert-preview__veil {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--ca-glass-bg);
  color: var(--ca-text-muted);
  font-family: var(--ca-font-mono);
  font-size: 14px;
}

.cert-preview__hint {
  color: var(--ca-text-muted);
  font-size: 14px;
  line-height: 1.5;
}

.cert-preview__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  flex-wrap: wrap;
}
</style>
