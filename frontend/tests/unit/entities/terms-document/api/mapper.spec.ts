import { describe, expect, it } from 'vitest'

import { toTermsDocument, toTermsDocumentRequest } from '@/entities/terms-document/api/mapper'

import { buildTermsDocument, richText } from '../../../../support/builders'

describe('terms document mapper', () => {
  it('maps a terms document', () => {
    expect(toTermsDocument(buildTermsDocument())).toEqual({
      id: 'terms-1',
      name: 'Privacy',
      description: richText('Terms body'),
    })
  })

  it('sends the name and the content', () => {
    expect(toTermsDocumentRequest({ name: 'Cookies', description: richText('Body') })).toEqual({
      name: 'Cookies',
      description: richText('Body'),
    })
  })
})
