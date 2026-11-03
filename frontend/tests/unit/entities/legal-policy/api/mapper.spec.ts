import { describe, expect, it } from 'vitest'

import { toLegalPolicy } from '@/entities/legal-policy/api/mapper'

import { buildLegalPolicy, richText } from '../../../../support/builders'

describe('legal policy mapper', () => {
  it('maps a version with its content', () => {
    expect(toLegalPolicy(buildLegalPolicy({ kind: 'Cookies' }))).toEqual({
      kind: 'Cookies',
      version: 2,
      content: richText('Policy body'),
      publishedAt: '2026-10-20T08:30:00Z',
    })
  })
})
