import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { accountKeys, accountMutations } from '@/entities/account'

import { http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildDependentResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const MINOR = {
  firstName: 'Byron',
  lastName: 'Lovelace',
  birthDate: '2015-03-02',
  gender: 'Male',
} as const

function cachedAccount() {
  const client = createTestQueryClient()
  const keys = [
    accountKeys.profile(),
    [...accountKeys.children(), 'user-1'],
    accountKeys.history(),
    accountKeys.certificates(),
  ]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('accountMutations', () => {
  it('refreshes the children after a household change', async () => {
    server.use(
      http.post('/api/users/:userId/children', () => HttpResponse.json(buildDependentResponse())),
      http.put('/api/users/:userId', () => HttpResponse.json(buildDependentResponse())),
      http.delete('/api/users/:userId', () => noContent()),
    )
    const runs = [
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, accountMutations.addChild('user-1')).mutate(MINOR),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, accountMutations.updateChild('user-1')).mutate({
          childId: 'child-1',
          input: MINOR,
        }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, accountMutations.removeChild()).mutate('child-1'),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedAccount()
      await run(client)
      expect(invalidated()).toEqual([false, true, false, false])
    }
  })

  it('refreshes the profile after a second-factor or email change and the history after a rating', async () => {
    server.use(
      http.post('/api/auth/two-factor/authenticator/confirm', () => noContent()),
      http.post('/api/auth/two-factor/email', () => noContent()),
      http.patch('/api/auth/:userId/confirm-email', () => noContent()),
      http.post('/api/events/:eventId/rating', () => noContent()),
    )

    const confirmed = cachedAccount()
    await new MutationObserver(confirmed.client, accountMutations.confirmAuthenticator()).mutate(
      '123456',
    )
    const disabled = cachedAccount()
    await new MutationObserver(disabled.client, accountMutations.disableAuthenticator()).mutate({
      currentPassword: 'Str0ngPass!23',
      code: '123456',
    })
    const emailConfirmed = cachedAccount()
    await new MutationObserver(emailConfirmed.client, accountMutations.confirmEmailChange()).mutate(
      { userId: 'user-1', code: 'code-1' },
    )
    const rated = cachedAccount()
    await new MutationObserver(rated.client, accountMutations.rateEvent()).mutate({
      eventId: 'event-1',
      input: { score: 5, mostLiked: '', leastLiked: '', suggestions: '' },
    })

    expect(confirmed.invalidated()).toEqual([true, false, false, false])
    expect(disabled.invalidated()).toEqual([true, false, false, false])
    expect(emailConfirmed.invalidated()).toEqual([true, false, false, false])
    expect(rated.invalidated()).toEqual([false, false, true, false])
  })
})
