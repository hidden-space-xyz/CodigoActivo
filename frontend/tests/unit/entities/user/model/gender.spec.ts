import { describe, expect, it } from 'vitest'

import { GENDERS, genderLabelKey } from '@/entities/user'

import { t } from '../../../../support/render'

describe('gender', () => {
  it('lists every gender in a fixed order', () => {
    expect(GENDERS).toEqual(['Male', 'Female', 'Other', 'PreferNotToSay'])
  })

  it('names the label of each gender', () => {
    expect(GENDERS.map((gender) => t(genderLabelKey(gender)))).toEqual([
      'Hombre',
      'Mujer',
      'Otro',
      'Prefiero no decirlo',
    ])
  })
})
