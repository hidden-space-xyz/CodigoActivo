import { useConfirm } from 'primevue/useconfirm'

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
