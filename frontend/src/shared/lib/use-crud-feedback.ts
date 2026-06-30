import { useToast } from 'primevue/usetoast'

export function useCrudFeedback() {
  const toast = useToast()

  function success(detail: string): void {
    toast.add({ severity: 'success', summary: 'Hecho', detail, life: 3000 })
  }

  function error(detail: string): void {
    toast.add({ severity: 'error', summary: 'Error', detail, life: 5000 })
  }

  return { success, error }
}
