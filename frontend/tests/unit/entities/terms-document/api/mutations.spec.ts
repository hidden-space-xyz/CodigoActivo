import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { termsDocumentKeys, termsDocumentMutations } from '@/entities/terms-document'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildTermsDocument, richText } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const INPUT = { name: 'Cookies', description: richText('Body') }

function cachedTerms() {
  const client = createTestQueryClient()
  const keys = [termsDocumentKeys.options(), [...termsDocumentKeys.list(), { page: 1 }]]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('termsDocumentMutations', () => {
  it('refreshes every terms query after creating, updating or deleting', async () => {
    server.use(
      http.post('/api/events/termsDocument', () =>
        HttpResponse.json(buildTermsDocument(), { status: 201 }),
      ),
      http.put('/api/events/termsDocument/:id', () => noContent()),
      http.delete('/api/events/termsDocument/:id', () => noContent()),
    )

    const created = cachedTerms()
    await new MutationObserver(created.client, termsDocumentMutations.create()).mutate(INPUT)
    const updated = cachedTerms()
    await new MutationObserver(updated.client, termsDocumentMutations.update()).mutate({
      id: 'terms-1',
      input: INPUT,
    })
    const removed = cachedTerms()
    await new MutationObserver(removed.client, termsDocumentMutations.remove()).mutate('terms-1')

    for (const { invalidated } of [created, updated, removed]) {
      expect(invalidated()).toEqual([true, true])
    }
  })

  it('leaves the terms queries alone when the API refuses the change', async () => {
    server.use(http.delete('/api/events/termsDocument/:id', () => apiError(409)))
    const { client, invalidated } = cachedTerms()

    await expect(
      new MutationObserver(client, termsDocumentMutations.remove()).mutate('terms-1'),
    ).rejects.toMatchObject({ status: 409 })

    expect(invalidated()).toEqual([false, false])
  })
})
