import { describe, expect, it } from 'vitest'

import { toPartner, toSponsor } from '@/entities/partner/api/mapper'

import { buildPartnerResponse, omit } from '../../../../support/builders'

describe('toPartner', () => {
  it('maps a partner of the admin list', () => {
    expect(toPartner(buildPartnerResponse())).toEqual({
      id: 'partner-1',
      name: 'Acme',
      fromDate: '2024-03-15',
      tier: 1,
      website: 'https://acme.test',
      thumbnailId: 'thumb-partner-1',
    })
  })

  it('reads a missing website as none', () => {
    expect(toPartner(omit(buildPartnerResponse(), 'website')).website).toBeNull()
    expect(toPartner(buildPartnerResponse({ website: null })).website).toBeNull()
  })

  it('never turns a website that is not http(s) into a link', () => {
    const website = 'javascript:alert(1)'

    expect(toPartner(buildPartnerResponse({ website })).website).toBeNull()
    expect(toSponsor(buildPartnerResponse({ website })).website).toBe('')
  })
})

describe('toSponsor', () => {
  it('maps what the sponsors section shows, a missing website as empty text', () => {
    expect(toSponsor(buildPartnerResponse({ website: null }))).toEqual({
      id: 'partner-1',
      name: 'Acme',
      website: '',
      thumbnailId: 'thumb-partner-1',
    })
  })
})
