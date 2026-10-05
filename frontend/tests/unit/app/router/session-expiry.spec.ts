import { defineComponent, h } from 'vue'
import { MutationObserver, type QueryClient } from '@tanstack/vue-query'
import { describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, type RouteMeta } from 'vue-router'

import { installSessionExpiry } from '@/app/router/session-expiry'
import { currentUser, startSession } from '@/entities/session'
import { ApiError } from '@/shared/api'

import { notifications } from '../../../support/dom'
import { buildAuthUser } from '../../../support/models'
import { createTestQueryClient, t } from '../../../support/render'

const Page = defineComponent({ render: () => h('div') })

async function setup(path: string, options: { guest?: boolean } = {}) {
  const queryClient = createTestQueryClient()
  if (!options.guest) startSession(queryClient, buildAuthUser())
  const route = (routePath: string, name: string, meta: RouteMeta = {}) => ({
    path: routePath,
    name,
    meta,
    component: Page,
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      route('/', 'home'),
      route('/login', 'login', { access: 'guest' }),
      route('/account', 'account', { access: 'user' }),
      route('/admin/users', 'admin-users', { access: 'admin' }),
    ],
  })
  installSessionExpiry(router, queryClient)
  await router.push(path)
  await router.isReady()
  return { queryClient, router }
}

function lostSession(): ApiError {
  return new ApiError(401, 'Error 401', undefined, 'AuthenticationRequired')
}

let queries = 0

async function failQuery(queryClient: QueryClient, error: Error): Promise<void> {
  queries += 1
  await queryClient
    .fetchQuery({ queryKey: ['failing', queries], queryFn: () => Promise.reject(error) })
    .catch(() => undefined)
}

async function failMutation(queryClient: QueryClient, error: Error): Promise<void> {
  const observer = new MutationObserver(queryClient, { mutationFn: () => Promise.reject(error) })
  await observer.mutate().catch(() => undefined)
}

function sessionEndedNotices(): number {
  return notifications().filter((item) => item.title === t('common.sessionEnded.title')).length
}

describe('installSessionExpiry', () => {
  it('ends the session and sends a protected page to login with a way back', async () => {
    const { queryClient, router } = await setup('/account?tab=history')

    await failQuery(queryClient, lostSession())

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(router.currentRoute.value.query).toEqual({ redirect: '/account?tab=history' })
    expect(currentUser(queryClient)).toBeNull()
    expect(notifications()).toContainEqual({
      title: t('common.sessionEnded.title'),
      message: t('common.sessionEnded.message'),
    })
  })

  it('reacts to a mutation the API refuses for a lost session', async () => {
    const { queryClient, router } = await setup('/admin/users')

    await failMutation(queryClient, new ApiError(401, 'x', undefined, 'CurrentUserNotFound'))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(currentUser(queryClient)).toBeNull()
  })

  it('keeps a public page, which then shows its guest view', async () => {
    const { queryClient, router } = await setup('/')

    await failQuery(queryClient, lostSession())

    await vi.waitFor(() => expect(currentUser(queryClient)).toBeNull())
    expect(router.currentRoute.value.name).toBe('home')
    expect(sessionEndedNotices()).toBe(1)
  })

  it('tells the user once when several requests fail together', async () => {
    const { queryClient } = await setup('/account')

    await Promise.all([
      failQuery(queryClient, lostSession()),
      failQuery(queryClient, lostSession()),
      failMutation(queryClient, lostSession()),
    ])

    await vi.waitFor(() => expect(currentUser(queryClient)).toBeNull())
    expect(sessionEndedNotices()).toBe(1)
  })

  it('ignores other failures and guests', async () => {
    const signedIn = await setup('/account')
    await failQuery(signedIn.queryClient, new ApiError(500, 'Error 500'))
    await failMutation(
      signedIn.queryClient,
      new ApiError(401, 'x', undefined, 'InvalidCredentials'),
    )

    const guest = await setup('/', { guest: true })
    await failQuery(guest.queryClient, lostSession())

    expect(currentUser(signedIn.queryClient)).not.toBeNull()
    expect(signedIn.router.currentRoute.value.name).toBe('account')
    expect(guest.router.currentRoute.value.name).toBe('home')
    expect(sessionEndedNotices()).toBe(0)
  })
})
