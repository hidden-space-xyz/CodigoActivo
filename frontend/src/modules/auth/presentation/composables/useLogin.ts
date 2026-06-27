import { reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation } from '@tanstack/vue-query'

import { postApiAuthLogin } from '@/shared/api/generated/endpoints/auth/auth'
import type { LoginRequest } from '@/shared/api/generated/models'
import { ApiError, resetCsrfToken } from '@/shared/api/http-client'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useLogin() {
  const session = useSessionStore()
  const router = useRouter()
  const route = useRoute()

  const form = reactive<LoginRequest>({ identifier: '', password: '' })

  const mutation = useMutation({
    mutationFn: (payload: LoginRequest) => postApiAuthLogin(payload),
    onSuccess: (response) => {
      session.setUser(response.data)
      resetCsrfToken()
      const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
      void router.push(redirect ?? { name: 'admin-dashboard' })
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
    error: mutation.error as unknown as ApiError | null,
  }
}
