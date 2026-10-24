import { describe, expect, it } from 'vitest'

import {
  getLegalPolicyRequest,
  getLegalPolicyVersionRequest,
  getLegalPolicyVersionsRequest,
  publishLegalPolicyRequest,
} from '@/entities/legal-policy/api/requests'

import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildLegalPolicy, buildLegalPolicyVersion, richText } from '../../../../support/builders'

describe('legal policy requests', () => {
  it('loads the version in force, or null while none was published', async () => {
    server.use(
      http.get('/api/legal-policies/Privacy', () => HttpResponse.json(buildLegalPolicy())),
      http.get('/api/legal-policies/Cookies', () => apiError(404)),
    )

    await expect(getLegalPolicyRequest('Privacy')).resolves.toMatchObject({ version: 2 })
    await expect(getLegalPolicyRequest('Cookies')).resolves.toBeNull()
  })

  it('lists the versions, or none while the policy was never published', async () => {
    server.use(
      http.get('/api/legal-policies/Privacy/versions', () =>
        HttpResponse.json([
          buildLegalPolicyVersion({ version: 2 }),
          buildLegalPolicyVersion({ version: 1 }),
        ]),
      ),
      http.get('/api/legal-policies/Cookies/versions', () => apiError(404)),
    )

    await expect(getLegalPolicyVersionsRequest('Privacy')).resolves.toEqual([
      { version: 2, publishedAt: '2026-09-01T08:30:00Z' },
      { version: 1, publishedAt: '2026-09-01T08:30:00Z' },
    ])
    await expect(getLegalPolicyVersionsRequest('Cookies')).resolves.toEqual([])
  })

  it('loads one version and publishes a new one', async () => {
    const bodies: unknown[] = []
    server.use(
      http.get('/api/legal-policies/Cookies/versions/1', () =>
        HttpResponse.json(buildLegalPolicy({ kind: 'Cookies', version: 1 })),
      ),
      http.put('/api/legal-policies/Cookies', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildLegalPolicy({ kind: 'Cookies', version: 3 }))
      }),
    )

    await expect(getLegalPolicyVersionRequest('Cookies', 1)).resolves.toMatchObject({
      kind: 'Cookies',
      version: 1,
    })
    await publishLegalPolicyRequest('Cookies', richText('New'))

    expect(bodies).toEqual([{ content: richText('New') }])
  })
})
