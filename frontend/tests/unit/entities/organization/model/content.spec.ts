import { describe, expect, it } from 'vitest'

import { useOrganizationContent } from '@/entities/organization'

import { t } from '../../../../support/render'

describe('useOrganizationContent', () => {
  it('returns the four translated organization values', () => {
    const { values } = useOrganizationContent()

    expect(values.map((value) => value.id)).toEqual(['free', 'inclusive', 'community', 'fun'])
    for (const value of values) {
      expect(value.title).toBe(t(`entities.organization.values.${value.id}.title`))
      expect(value.description).toBe(t(`entities.organization.values.${value.id}.description`))
      expect(value.icon).not.toBe('')
      expect(value.soft).toMatch(/^rgba\(/)
    }
  })

  it('returns the four activities numbered in order with their colors', () => {
    const { activities } = useOrganizationContent()

    expect(activities.map((activity) => [activity.id, activity.number])).toEqual([
      ['workshops', '01'],
      ['annualDay', '02'],
      ['meetAndCode', '03'],
      ['competitions', '04'],
    ])
    for (const activity of activities) {
      expect(activity.title).toBe(t(`entities.organization.activities.${activity.id}.title`))
      expect(activity.description).toBe(
        t(`entities.organization.activities.${activity.id}.description`),
      )
      expect(activity.color).toMatch(/^#[0-9A-F]{6}$/i)
    }
  })

  it('returns the same static content on every call', () => {
    expect(useOrganizationContent().values).toBe(useOrganizationContent().values)
  })
})
