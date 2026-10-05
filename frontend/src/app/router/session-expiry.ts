import { ElNotification } from 'element-plus'
import type { QueryClient } from '@tanstack/vue-query'
import type { Router } from 'vue-router'

import { currentUser, endSession } from '@/entities/session'
import { isSessionLost, resetCsrfToken } from '@/shared/api'
import { i18n } from '@/shared/i18n'

/**
 * Ends the local session as soon as a query or mutation reports that the API no longer accepts it,
 * for example after the password changed or the account was blocked from elsewhere. The user is
 * told once; on a page that needs a session they go to login with a `redirect` back, while public
 * pages simply show their guest view. Guests are left alone.
 */
export function installSessionExpiry(router: Router, queryClient: QueryClient): void {
  let ending = false

  async function expire(): Promise<void> {
    if (ending || !currentUser(queryClient)) return
    ending = true
    try {
      endSession(queryClient)
      resetCsrfToken()
      ElNotification({
        type: 'warning',
        title: i18n.global.t('common.sessionEnded.title'),
        message: i18n.global.t('common.sessionEnded.message'),
        duration: 6000,
        position: 'top-right',
      })
      const route = router.currentRoute.value
      if (route.meta.access === 'user' || route.meta.access === 'admin') {
        await router.push({ name: 'login', query: { redirect: route.fullPath } })
      }
    } finally {
      ending = false
    }
  }

  function onError(error: unknown): void {
    if (isSessionLost(error)) void expire()
  }

  queryClient.getQueryCache().subscribe((event) => {
    if (event.type === 'updated' && event.action.type === 'error') onError(event.action.error)
  })
  queryClient.getMutationCache().subscribe((event) => {
    if (event.type === 'updated' && event.action.type === 'error') onError(event.action.error)
  })
}
