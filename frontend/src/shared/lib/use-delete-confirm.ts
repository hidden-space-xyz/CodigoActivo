import { useConfirm } from 'primevue/useconfirm'

/**
 * The app-standard destructive-action confirmation (warning icon, Eliminar/Cancelar, danger
 * accept). Call sites provide only the header, message, and what to do on accept.
 */
export function useDeleteConfirm() {
  const confirm = useConfirm()

  function confirmDelete(options: { header: string; message: string; accept: () => void }): void {
    confirm.require({
      ...options,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      acceptClass: 'p-button-danger',
    })
  }

  return { confirmDelete }
}
