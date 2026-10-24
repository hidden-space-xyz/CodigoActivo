import { describe, expect, it } from 'vitest'

import { legalPolicyKeys, legalPolicyQueries } from '@/entities/legal-policy'

import { http, HttpResponse, server } from '../../../../support/server'
import { buildLegalPolicy, buildLegalPolicyVersion } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('legal policy queries', () => {
  it('nests every query of a policy under one root', () => {
    expect(legalPolicyKeys.current('Privacy')).toEqual(['legal-policies', 'Privacy', 'current'])
    expect(legalPolicyKeys.versions('Cookies')).toEqual(['legal-policies', 'Cookies', 'versions'])
    expect(legalPolicyKeys.version('Cookies', 3)).toEqual([
      'legal-policies',
      'Cookies',
      'versions',
      3,
    ])
  })

  it('loads the version in force, the versions and one version', async () => {
    server.use(
      http.get('/api/legal-policies/Privacy', () => HttpResponse.json(buildLegalPolicy())),
      http.get('/api/legal-policies/Privacy/versions', () =>
        HttpResponse.json([buildLegalPolicyVersion()]),
      ),
      http.get('/api/legal-policies/Privacy/versions/1', () =>
        HttpResponse.json(buildLegalPolicy({ version: 1 })),
      ),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(legalPolicyQueries.current('Privacy'))).resolves.toMatchObject({
      version: 2,
    })
    await expect(client.fetchQuery(legalPolicyQueries.versions('Privacy'))).resolves.toEqual([
      { version: 1, publishedAt: '2026-09-01T08:30:00Z' },
    ])
    await expect(
      client.fetchQuery(legalPolicyQueries.version('Privacy', 1)),
    ).resolves.toMatchObject({ version: 1 })
  })
})
