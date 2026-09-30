import { describe, expect, it } from 'vitest'

import { readPartnerDraft, toPartnerDraft } from '@/pages/admin/partners/model/partner-form'

import { buildPartner } from '../../../../../support/models'

describe('toPartnerDraft', () => {
  it('starts blank with tier zero', () => {
    expect(toPartnerDraft(null)).toEqual({ name: '', fromDate: null, tier: 0, website: '' })
  })

  it('fills the draft from the partner being edited', () => {
    expect(toPartnerDraft(buildPartner({ website: null }))).toEqual({
      name: 'Acme',
      fromDate: new Date(2024, 2, 15),
      tier: 1,
      website: '',
    })
  })
})

describe('readPartnerDraft', () => {
  it('requires a name and a start day', () => {
    expect(readPartnerDraft({ name: '  ', fromDate: null, tier: 0, website: '' })).toEqual({
      problems: {
        name: 'pages.admin.partners.form.problems.nameRequired',
        fromDate: 'pages.admin.partners.form.problems.fromDateRequired',
      },
      value: null,
    })
  })

  it('trims the text, reads an empty tier as zero and a blank website as none', () => {
    expect(
      readPartnerDraft({
        name: ' Initech ',
        fromDate: new Date(2025, 5, 7),
        tier: undefined,
        website: '  ',
      }),
    ).toEqual({
      problems: {},
      value: { name: 'Initech', fromDate: '2025-06-07', tier: 0, website: null },
    })
  })

  it('keeps the trimmed website', () => {
    expect(
      readPartnerDraft({
        name: 'Initech',
        fromDate: new Date(2025, 5, 7),
        tier: 3,
        website: ' https://initech.test ',
      }).value,
    ).toMatchObject({ tier: 3, website: 'https://initech.test' })
  })
})
