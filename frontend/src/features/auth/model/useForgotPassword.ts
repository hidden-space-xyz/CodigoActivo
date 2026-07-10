import { reactive, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'

import { forgotPasswordRequest } from '../api/requests'

export function useForgotPassword() {
  const form = reactive({ email: '' })
  const sent = ref(false)

  const mutation = useMutation({
    mutationFn: (email: string) => forgotPasswordRequest(email),
    onSuccess: () => {
      sent.value = true
    },
  })

  function submit(): void {
    mutation.mutate(form.email.trim())
  }

  return { form, sent, submit, isSubmitting: mutation.isPending, isError: mutation.isError }
}
