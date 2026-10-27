import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useMutation } from '@tanstack/vue-query'

import { createEmptyCredentials, loginRequest } from '@/entities/session'
import type { Credentials } from '@/entities/session'
import { hasErrorCode } from '@/shared/api'
import { getErrorMessage } from '@/shared/lib/feedback'
import { toLocalRedirect } from '@/shared/lib/navigation'

/**
 * Login form (password step). A correct password opens a second-factor challenge on the server,
 * so on success the user is sent to the verification page, carrying the local `redirect` query
 * parameter along so the final destination survives both steps. A failure sets `errorMessage`:
 * wrong credentials and failures without a known code keep the generic login message, while the
 * account states the API only reveals after a correct password (unverified, blocked, second
 * factor locked) and other coded refusals show their own message.
 */
export function useLogin() {
  const { t } = useI18n()
  const router = useRouter()
  const route = useRoute()

  const form = reactive<Credentials>(createEmptyCredentials())

  const mutation = useMutation({
    mutationFn: (credentials: Credentials) => loginRequest(credentials),
    onSuccess: () => {
      const redirect = toLocalRedirect(route.query.redirect)
      void router.push({
        name: 'login-two-factor',
        ...(redirect ? { query: { redirect } } : {}),
      })
    },
  })

  const errorMessage = computed(() => {
    const error = mutation.error.value
    if (!error) return null
    const generic = t('pages.login.error')
    return hasErrorCode(error, 'InvalidCredentials') ? generic : getErrorMessage(error, generic)
  })

  function submit(): void {
    mutation.mutate({ ...form })
  }

  return {
    form,
    submit,
    isSubmitting: mutation.isPending,
    errorMessage,
  }
}
