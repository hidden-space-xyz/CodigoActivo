<script setup lang="ts">
import Toast from 'primevue/toast'

import type { ErrorToastMessageOptions } from '@/shared/lib'

const SEVERITY_ICON: Record<string, string> = {
  success: 'pi-check',
  info: 'pi-info-circle',
  warn: 'pi-exclamation-triangle',
  error: 'pi-times-circle',
  secondary: 'pi-info-circle',
  contrast: 'pi-info-circle',
}
</script>

<template>
  <Toast>
    <template #message="slotProps">
      <div class="app-toast">
        <i class="pi app-toast__icon" :class="SEVERITY_ICON[slotProps.message.severity ?? 'info']" />
        <div class="app-toast__text">
          <span class="app-toast__summary">{{ slotProps.message.summary }}</span>
          <div v-if="slotProps.message.detail" class="app-toast__detail">
            {{ slotProps.message.detail }}
          </div>
          <div
            v-if="(slotProps.message as ErrorToastMessageOptions).traceId"
            class="app-toast__trace"
          >
            Ref: {{ (slotProps.message as ErrorToastMessageOptions).traceId }}
          </div>
        </div>
      </div>
    </template>
  </Toast>
</template>

<style scoped>
.app-toast {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.app-toast__icon {
  margin-top: 2px;
  font-size: 1.2rem;
  flex-shrink: 0;
}

.app-toast__text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.app-toast__summary {
  font-weight: 600;
}

.app-toast__detail {
  margin-top: 2px;
}

.app-toast__trace {
  margin-top: 4px;
  font-size: 11px;
  opacity: 0.7;
}
</style>
