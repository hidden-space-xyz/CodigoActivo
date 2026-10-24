import { describe, expect, it } from 'vitest'

import { toLegalPolicy, toLegalPolicyVersion } from '@/entities/legal-policy/api/mapper'

import { buildLegalPolicy, buildLegalPolicyVersion, richText } from '../../../../support/builders'

describe('legal policy mapper', () => {
  it('maps a version with its content', () => {
    expect(toLegalPolicy(buildLegalPolicy({ kind: 'Cookies' }))).toEqual({
      kind: 'Cookies',
      version: 2,
      content: richText('Policy body'),
      publishedAt: '2026-10-20T08:30:00Z',
    })
  })

  it('maps when a version was published', () => {
    expect(toLegalPolicyVersion(buildLegalPolicyVersion())).toEqual({
      version: 1,
      publishedAt: '2026-09-01T08:30:00Z',
    })
  })
})
