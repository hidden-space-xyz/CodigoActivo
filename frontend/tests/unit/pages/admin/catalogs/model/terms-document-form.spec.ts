import { describe, expect, it } from 'vitest'

import {
  readTermsDocumentDraft,
  toTermsDocumentDraft,
} from '@/pages/admin/catalogs/model/terms-document-form'

import { richText } from '../../../../../support/builders'

describe('toTermsDocumentDraft', () => {
  it('starts blank and fills an edited terms document', () => {
    expect(toTermsDocumentDraft(null)).toEqual({ name: '', description: '' })
    expect(
      toTermsDocumentDraft({ id: 'terms-1', name: 'Privacy', description: richText('Body') }),
    ).toEqual({ name: 'Privacy', description: richText('Body') })
  })
})

describe('readTermsDocumentDraft', () => {
  it('refuses a blank name without a message and asks for some content', () => {
    expect(readTermsDocumentDraft({ name: ' ', description: '' })).toEqual({
      problems: {
        name: true,
        description: 'pages.admin.catalogs.termsDocuments.form.problems.contentRequired',
      },
      value: null,
    })
  })

  it('trims the name and keeps the content', () => {
    expect(readTermsDocumentDraft({ name: ' Cookies ', description: richText('Body') })).toEqual({
      problems: {},
      value: { name: 'Cookies', description: richText('Body') },
    })
  })
})
