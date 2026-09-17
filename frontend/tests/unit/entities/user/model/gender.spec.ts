import { describe, expect, it } from 'vitest'

import { genderLabel, genderOptions } from '@/entities/user'

import { t } from '../../../../support/render'

describe('gender', () => {
  it.each(['Male', 'Female', 'Other'] as const)('translates %s', (gender) => {
    expect(genderLabel(gender)).toBe(t(`entities.user.gender.${gender}`))
  })

  it('builds the select options in a fixed order', () => {
    expect(genderOptions()).toEqual([
      { label: 'Hombre', value: 'Male' },
      { label: 'Mujer', value: 'Female' },
      { label: 'Otro', value: 'Other' },
    ])
  })
})
