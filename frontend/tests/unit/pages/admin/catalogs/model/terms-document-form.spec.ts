import { describe, expect, it } from 'vitest'

import {
  readTermsDocumentDraft,
  toTermsDocumentDraft,
} from '@/pages/admin/catalogs/model/terms-document-form'

import { richText } from '../../../../../support/builders'

describe('toTermsDocumentDraft', () => {
  it('starts blank and fills an edited terms document', () => {
    expect(toTermsDocumentDraft(null)).toEqual({
      name: '',
      description: '',
      requiresSignature: false,
      kind: 'Conditions',
    })
    expect(
      toTermsDocumentDraft({
        id: 'terms-1',
        name: 'Privacy',
        description: richText('Body'),
        requiresSignature: true,
        kind: 'Conditions',
      }),
    ).toEqual({
      name: 'Privacy',
      description: richText('Body'),
      requiresSignature: true,
      kind: 'Conditions',
    })
  })
})

describe('readTermsDocumentDraft', () => {
  it('refuses a blank name without a message and asks for some content', () => {
    expect(
      readTermsDocumentDraft({
        name: ' ',
        description: '',
        requiresSignature: false,
        kind: 'Conditions',
      }),
    ).toEqual({
      problems: {
        name: true,
        description: 'pages.admin.catalogs.termsDocuments.form.problems.contentRequired',
      },
      value: null,
    })
  })

  it('refuses content longer than the API stores', () => {
    expect(
      readTermsDocumentDraft({
        name: 'Cookies',
        description: richText('a'.repeat(262_144)),
        requiresSignature: false,
        kind: 'Conditions',
      }),
    ).toEqual({ problems: { description: 'validation.richTextTooLong' }, value: null })
  })

  it('trims the name and keeps the content and whether it asks for a signature', () => {
    expect(
      readTermsDocumentDraft({
        name: ' Cookies ',
        description: richText('Body'),
        requiresSignature: true,
        kind: 'Conditions',
      }),
    ).toEqual({
      problems: {},
      value: {
        name: 'Cookies',
        description: richText('Body'),
        requiresSignature: true,
        kind: 'Conditions',
      },
    })
  })
})
