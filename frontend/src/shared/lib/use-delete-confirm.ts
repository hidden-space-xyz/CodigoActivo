import { useConfirm } from 'primevue/useconfirm'

import { i18n } from '@/shared/i18n'

export function useDeleteConfirm() {
  const confirm = useConfirm()

  function confirmDelete(options: { header: string; message: string; accept: () => void }): void {
    confirm.require({
      ...options,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: i18n.global.t('common.delete'),
      rejectLabel: i18n.global.t('common.cancel'),
      acceptClass: 'p-button-danger',
    })
  }

  return { confirmDelete }
}

export function useActionConfirm() {
  const confirm = useConfirm()

  function confirmAction(options: {
    header: string
    message: string
    acceptLabel: string
    accept: () => void
  }): void {
    confirm.require({
      ...options,
      icon: 'pi pi-exclamation-triangle',
      rejectLabel: i18n.global.t('common.cancel'),
    })
  }

  return { confirmAction }
}
