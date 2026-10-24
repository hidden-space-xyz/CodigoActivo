import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { legalPolicyKeys, legalPolicyMutations } from '@/entities/legal-policy'

import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildLegalPolicy, richText } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

function cachedPolicies() {
  const client = createTestQueryClient()
  const keys = [legalPolicyKeys.current('Privacy'), legalPolicyKeys.versions('Cookies')]
  for (const queryKey of keys) client.setQueryData(queryKey, null)
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('legalPolicyMutations', () => {
  it('refreshes every policy query after publishing', async () => {
    server.use(http.put('/api/legal-policies/Privacy', () => HttpResponse.json(buildLegalPolicy())))
    const { client, invalidated } = cachedPolicies()

    await new MutationObserver(client, legalPolicyMutations.publish()).mutate({
      kind: 'Privacy',
      content: richText('New'),
    })

    expect(invalidated()).toEqual([true, true])
  })

  it('leaves the policy queries alone when the API refuses the content', async () => {
    server.use(http.put('/api/legal-policies/Privacy', () => apiError(400)))
    const { client, invalidated } = cachedPolicies()

    await expect(
      new MutationObserver(client, legalPolicyMutations.publish()).mutate({
        kind: 'Privacy',
        content: richText(''),
      }),
    ).rejects.toMatchObject({ status: 400 })

    expect(invalidated()).toEqual([false, false])
  })
})
