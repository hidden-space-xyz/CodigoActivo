import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { userKeys, userMutations } from '@/entities/user'

import { http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildUserResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

function cachedUsers() {
  const client = createTestQueryClient()
  const keys = [userKeys.list(), [...userKeys.all, 'other'], userKeys.types()]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('userMutations', () => {
  it('refreshes every user query, but not their catalogs, after each change', async () => {
    server.use(
      http.delete('/api/users/:id', () => noContent()),
      http.patch('/api/users/:id/change-type', () => HttpResponse.json(buildUserResponse())),
      http.patch('/api/users/:id/staff-role', () => noContent()),
      http.post('/api/users/:id/privacy-officer', () => noContent()),
      http.post('/api/users/:id/two-factor/reset', () => noContent()),
    )
    const runs = [
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, userMutations.remove()).mutate('u1'),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, userMutations.changeType()).mutate({
          id: 'u1',
          userTypeId: 'type-member',
        }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, userMutations.setStaffRole()).mutate({
          id: 'u1',
          staffRole: 'None',
          currentPassword: null,
        }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, userMutations.designatePrivacyOfficer()).mutate({
          id: 'u1',
          currentPassword: 'Str0ngPass!23',
        }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, userMutations.resetTwoFactor()).mutate({
          id: 'u1',
          currentPassword: 'Str0ngPass!23',
        }),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedUsers()
      await run(client)
      expect(invalidated()).toEqual([true, true, false])
    }
  })
})
