import { describe, expect, it } from 'vitest'

import { useLogout } from '@/features/logout'

import { mountComposable } from '../../../../support/render'
import { apiError, http, noContent, server, TEST_CSRF_TOKEN } from '../../../../support/server'
import { sessionOf } from '../../../../support/session'

describe('useLogout', () => {
  it('ends the session on the API, forgets the cached data and goes to login', async () => {
    let csrf: string | null = null
    server.use(
      http.post('/api/auth/logout', ({ request }) => {
        csrf = request.headers.get('X-CSRF-TOKEN')
        return noContent()
      }),
    )
    const { result, router, queryClient } = await mountComposable(() => useLogout(), {
      user: { firstName: 'Grace' },
      route: '/about',
    })
    queryClient.setQueryData(['account', 'me'], { id: 'user-1' })

    await result()

    expect(csrf).toBe(TEST_CSRF_TOKEN)
    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
    expect(queryClient.getQueryData(['account', 'me'])).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('still drops the local session when the API call fails', async () => {
    server.use(http.post('/api/auth/logout', () => apiError(500)))
    const { result, router, queryClient } = await mountComposable(() => useLogout(), {
      user: { firstName: 'Grace' },
      route: '/about',
    })

    await expect(result()).rejects.toMatchObject({ status: 500 })

    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })
})
