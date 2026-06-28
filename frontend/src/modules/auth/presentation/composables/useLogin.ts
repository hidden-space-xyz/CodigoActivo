import { reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMutation } from '@tanstack/vue-query'

import { login as loginUseCase } from '@/modules/auth/application/use-cases/login.use-case'
import {
  createEmptyCredentials,
  type Credentials,
} from '@/modules/auth/domain/value-objects/credentials'
import { authRepository } from '@/modules/auth/infrastructure/repositories/auth-repository.provider'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

export function useLogin() {
  const session = useSessionStore()
  const router = useRouter()
  const route = useRoute()

  const form = reactive<Credentials>(createEmptyCredentials())

  const mutation = useMutation({
    mutationFn: (credentials: Credentials) => loginUseCase(authRepository, credentials),
    onSuccess: (user) => {
      session.setUser(user)
      const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : null
      void router.push(redirect ?? { name: 'home' })
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
  }
}
