import { h, type VNode } from 'vue'
import { ElNotification } from 'element-plus'
import { useI18n } from 'vue-i18n'

import { ApiError } from '@/shared/api'
import { getErrorMessage } from './api-error'

const TRACE_STYLE = {
  marginTop: '4px',
  fontFamily: 'var(--ca-font-mono)',
  fontSize: '11px',
  color: 'var(--ca-text-faint)',
  overflowWrap: 'anywhere',
} as const

function errorMessage(detail: string, trace: string): VNode {
  return h('div', [h('div', detail), h('div', { style: TRACE_STYLE }, trace)])
}

export function useCrudFeedback() {
  const { t } = useI18n()

  function success(detail: string, summary = t('common.done')): void {
    ElNotification({
      type: 'success',
      title: summary,
      message: detail,
      duration: 3000,
      position: 'top-right',
    })
  }

  function warn(detail: string, summary = t('common.warning')): void {
    ElNotification({
      type: 'warning',
      title: summary,
      message: detail,
      duration: 6000,
      position: 'top-right',
    })
  }

  function error(err: unknown, summary = t('common.error')): void {
    const detail = getErrorMessage(err)
    const traceId = err instanceof ApiError ? err.traceId : undefined

    ElNotification({
      type: 'error',
      title: summary,
      message: traceId ? errorMessage(detail, t('table.ref', { id: traceId })) : detail,
      duration: 5000,
      position: 'top-right',
    })
  }

  return { success, warn, error }
}
