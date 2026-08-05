<script setup lang="ts">
import Toast from 'primevue/toast'

import type { ErrorToastMessageOptions } from '@/shared/lib'

const SEVERITY_ICON: Record<string, string> = {
  success: 'pi-check-circle',
  info: 'pi-info-circle',
  warn: 'pi-exclamation-triangle',
  error: 'pi-times-circle',
  secondary: 'pi-info-circle',
  contrast: 'pi-info-circle',
}

const TOAST_PT = { closeButton: { autofocus: null } }
</script>

<template>
  <Toast :pt="TOAST_PT">
    <template #message="slotProps">
      <div class="app-toast">
        <i
          class="pi p-toast-message-icon app-toast__icon"
          :class="SEVERITY_ICON[slotProps.message.severity ?? 'info']"
        />
        <div class="p-toast-message-text">
          <span class="p-toast-summary">{{ slotProps.message.summary }}</span>
          <div v-if="slotProps.message.detail" class="p-toast-detail">
            {{ slotProps.message.detail }}
          </div>
          <div
            v-if="(slotProps.message as ErrorToastMessageOptions).traceId"
            class="app-toast__trace"
          >
            {{ $t('table.ref', { id: (slotProps.message as ErrorToastMessageOptions).traceId }) }}
          </div>
        </div>
      </div>
    </template>
  </Toast>
</template>

<style scoped>
.app-toast {
  flex: 1 1 auto;
  min-width: 0;
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.app-toast__icon {
  margin-top: 3px;
  flex-shrink: 0;
}

.app-toast__trace {
  margin-top: 4px;
  font-family: var(--ca-font-mono);
  font-size: 11px;
  color: var(--ca-text-faint);
  overflow-wrap: anywhere;
}
</style>
