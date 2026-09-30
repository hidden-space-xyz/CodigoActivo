import { describe, expect, it } from 'vitest'

import {
  termsDocumentKeys,
  termsDocumentList,
  termsDocumentQueries,
} from '@/entities/terms-document'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { buildTermsDocument } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('terms document queries', () => {
  it('nests the options and the admin list under one root', () => {
    expect(termsDocumentKeys.options()).toEqual(['terms-documents', 'options'])
    expect(termsDocumentKeys.list()).toEqual(['terms-documents', 'list'])
    expect(termsDocumentList.queryKey).toEqual(termsDocumentKeys.list())
  })

  it('loads the options and pages the admin list', async () => {
    server.use(
      http.get('/api/events/termsDocument', () => HttpResponse.json(paged([buildTermsDocument()]))),
    )

    await expect(
      createTestQueryClient().fetchQuery(termsDocumentQueries.options()),
    ).resolves.toEqual([expect.objectContaining({ id: 'terms-1' })])
    await expect(termsDocumentList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 1 })
  })
})
