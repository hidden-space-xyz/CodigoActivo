import { ElMessageBox } from 'element-plus'

import { i18n } from '@/shared/i18n'

export function useDeleteConfirm() {
  function confirmDelete(options: { header: string; message: string; accept: () => void }): void {
    void ElMessageBox.confirm(options.message, options.header, {
      confirmButtonText: i18n.global.t('common.delete'),
      cancelButtonText: i18n.global.t('common.cancel'),
      confirmButtonClass: 'el-button--danger',
      type: 'warning',
    })
      .then(() => {
        options.accept()
      })
      .catch(() => undefined)
  }

  return { confirmDelete }
}

export function useActionConfirm() {
  function confirmAction(options: {
    header: string
    message: string
    acceptLabel: string
    accept: () => void
  }): void {
    void ElMessageBox.confirm(options.message, options.header, {
      confirmButtonText: options.acceptLabel,
      cancelButtonText: i18n.global.t('common.cancel'),
      type: 'warning',
    })
      .then(() => {
        options.accept()
      })
      .catch(() => undefined)
  }

  return { confirmAction }
}
