import { describe, expect, it } from 'vitest'

import { GENDERS, genderLabelKey } from '@/entities/user'

import { t } from '../../../../support/render'

describe('gender', () => {
  it('names the label of each gender', () => {
    expect(GENDERS.map((gender) => t(genderLabelKey(gender)))).toEqual([
      'Hombre',
      'Mujer',
      'Otro',
      'Prefiero no decirlo',
    ])
  })
})
