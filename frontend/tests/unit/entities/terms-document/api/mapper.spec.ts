import { describe, expect, it } from 'vitest'

import { toTermsDocument } from '@/entities/terms-document/api/mapper'

import { buildTermsDocument, richText } from '../../../../support/builders'

describe('terms document mapper', () => {
  it('maps a terms document', () => {
    expect(toTermsDocument(buildTermsDocument())).toEqual({
      id: 'terms-1',
      name: 'Privacy',
      description: richText('Terms body'),
      requiresSignature: false,
      kind: 'Conditions',
      version: 1,
    })
  })
})
