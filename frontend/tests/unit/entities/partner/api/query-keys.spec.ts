import { describe, expect, it } from 'vitest'

import { partnerQueryKeys } from '@/entities/partner'

describe('partnerQueryKeys', () => {
  it('shares the partners root between sponsors and the admin table', () => {
    expect(partnerQueryKeys.all).toEqual(['partners'])
    expect(partnerQueryKeys.sponsors()).toEqual(['partners', 'sponsors'])
    expect(partnerQueryKeys.adminTable()).toEqual(['partners', 'admin'])
  })
})
