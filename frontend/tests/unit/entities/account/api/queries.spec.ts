import { describe, expect, it } from 'vitest'

import { accountKeys, accountQueries } from '@/entities/account'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import {
  buildCertificateResponse,
  buildDependentResponse,
  buildHistoryResponse,
  buildUserResponse,
} from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('account queries', () => {
  it('nests every account query under one root, the children per guardian', () => {
    expect(accountKeys.profile()).toEqual(['account', 'profile'])
    expect(accountQueries.children('user-1').queryKey).toEqual(['account', 'children', 'user-1'])
    expect(accountKeys.history()).toEqual(['account', 'history'])
    expect(accountKeys.certificates()).toEqual(['account', 'certificates'])
    expect(accountKeys.deletionAllowed()).toEqual(['account', 'deletion-allowed'])
  })

  it('loads the profile, the children, the history, the certificates and the deletion rule', async () => {
    server.use(
      http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())),
      http.get('/api/users', () => HttpResponse.json(paged([buildDependentResponse()]))),
      http.get('/api/me/event-history', () => HttpResponse.json([buildHistoryResponse()])),
      http.get('/api/me/certificates', () => HttpResponse.json([buildCertificateResponse()])),
      http.get('/api/me/deletion', () => HttpResponse.json({ allowed: true })),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(accountQueries.profile())).resolves.toMatchObject({
      id: 'user-1',
    })
    await expect(client.fetchQuery(accountQueries.children('user-1'))).resolves.toEqual([
      expect.objectContaining({ id: 'child-1' }),
    ])
    await expect(client.fetchQuery(accountQueries.history())).resolves.toHaveLength(1)
    await expect(client.fetchQuery(accountQueries.certificates())).resolves.toHaveLength(1)
    await expect(client.fetchQuery(accountQueries.deletionAllowed())).resolves.toBe(true)
  })
})
