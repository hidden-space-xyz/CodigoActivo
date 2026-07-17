import { useToast } from 'primevue/usetoast'
import type { ToastMessageOptions } from 'primevue/toast'
import { useI18n } from 'vue-i18n'

import { ApiError } from '@/shared/api/http-client'
import { getErrorMessage } from './api-error'

export interface ErrorToastMessageOptions extends ToastMessageOptions {
  traceId?: string | undefined
}

export function useCrudFeedback() {
  const toast = useToast()
  const { t } = useI18n()

  function success(detail: string, summary = t('common.done')): void {
    toast.add({ severity: 'success', summary, detail, life: 3000 })
  }

  function error(err: unknown, summary = t('common.error')): void {
    const message: ErrorToastMessageOptions = {
      severity: 'error',
      summary,
      detail: getErrorMessage(err),
      life: 5000,
      traceId: err instanceof ApiError ? err.traceId : undefined,
    }
    toast.add(message)
  }

  return { success, error }
}
