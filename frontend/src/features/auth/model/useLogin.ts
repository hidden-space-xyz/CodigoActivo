import { reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation } from '@tanstack/vue-query'

import { createEmptyCredentials, loginRequest } from '@/entities/session'
import type { Credentials } from '@/entities/session'

/**
 * Login form (password step). A correct password opens a second-factor challenge on the server,
 * so on success the user is sent to the verification page, carrying the `redirect` query
 * parameter along so the final destination survives both steps.
 */
export function useLogin() {
  const router = useRouter()
  const route = useRoute()

  const form = reactive<Credentials>(createEmptyCredentials())

  const mutation = useMutation({
    mutationFn: (credentials: Credentials) => loginRequest(credentials),
    onSuccess: () => {
      const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
      void router.push({
        name: 'login-two-factor',
        ...(redirect ? { query: { redirect } } : {}),
      })
    },
  })

  function submit(): void {
    mutation.mutate({ ...form })
  }

  return {
    form,
    submit,
    isSubmitting: mutation.isPending,
    isError: mutation.isError,
    error: mutation.error,
  }
}
