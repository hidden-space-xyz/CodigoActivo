import { describe, expect, it } from 'vitest'

import { toTermsDocument, toTermsDocumentRequest } from '@/entities/terms-document/api/mapper'

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

  it('sends the name, the content and whether it asks for a signature', () => {
    expect(
      toTermsDocumentRequest({
        name: 'Cookies',
        description: richText('Body'),
        requiresSignature: true,
        kind: 'Conditions',
      }),
    ).toEqual({
      name: 'Cookies',
      description: richText('Body'),
      requiresSignature: true,
      kind: 'Conditions',
    })
  })
})
