import { ElMessageBox } from 'element-plus'

import { i18n } from '@/shared/i18n'

function confirm(options: {
  header: string
  message: string
  acceptLabel: string
  accept: () => void
  danger?: boolean
}): void {
  void ElMessageBox.confirm(options.message, options.header, {
    confirmButtonText: options.acceptLabel,
    cancelButtonText: i18n.global.t('common.cancel'),
    ...(options.danger ? { confirmButtonClass: 'el-button--danger' } : {}),
    type: 'warning',
  })
    .then(() => {
      options.accept()
    })
    .catch(() => undefined)
}

/** Warning dialog with a red "delete" button; `accept` runs only on confirmation. */
export function useDeleteConfirm() {
  function confirmDelete(options: { header: string; message: string; accept: () => void }): void {
    confirm({ ...options, acceptLabel: i18n.global.t('common.delete'), danger: true })
  }

  return { confirmDelete }
}

/** Warning dialog for non-destructive actions with a caller-provided confirm label. */
export function useActionConfirm() {
  function confirmAction(options: {
    header: string
    message: string
    acceptLabel: string
    accept: () => void
  }): void {
    confirm(options)
  }

  return { confirmAction }
}
