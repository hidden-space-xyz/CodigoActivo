import { describe, expect, it } from 'vitest'

import {
  isSameLegalPolicyContent,
  readLegalPolicyDraft,
  toLegalPolicyDraft,
} from '@/pages/admin/legal-policies/model/legal-policy-form'

import { buildLegalPolicy, richText } from '../../../../../support/builders'

describe('legal policy form', () => {
  it('starts blank, or from the version in force', () => {
    expect(toLegalPolicyDraft(null)).toEqual({ content: '' })
    expect(toLegalPolicyDraft(undefined)).toEqual({ content: '' })
    expect(toLegalPolicyDraft(buildLegalPolicy({ content: richText('Vigente') }))).toEqual({
      content: richText('Vigente'),
    })
  })

  it('compares the content, not how its JSON is laid out', () => {
    const stored =
      '{"content": [{"content": [{"text": "Igual", "type": "text"}], "type": "paragraph"}], "type": "doc"}'

    expect(isSameLegalPolicyContent(stored, richText('Igual'))).toBe(true)
    expect(isSameLegalPolicyContent(richText('Igual'), richText('Otra'))).toBe(false)
    expect(isSameLegalPolicyContent('', '{}')).toBe(true)
  })

  it('requires some text that fits what the API stores', () => {
    expect(readLegalPolicyDraft({ content: richText('  ') }).problems).toEqual({
      content: 'pages.admin.legalPolicies.form.problems.contentRequired',
    })
    expect(readLegalPolicyDraft({ content: richText('x'.repeat(262_144)) }).problems).toEqual({
      content: 'validation.richTextTooLong',
    })
    expect(readLegalPolicyDraft({ content: richText('Texto') })).toEqual({
      problems: {},
      value: { content: richText('Texto') },
    })
  })
})
