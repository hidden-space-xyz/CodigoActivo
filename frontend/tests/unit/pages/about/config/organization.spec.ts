import { describe, expect, it } from 'vitest'

import { ORGANIZATION_ACTIVITIES, ORGANIZATION_VALUES } from '@/pages/about/config/organization'

import { t } from '../../../../support/render'

describe('organization content', () => {
  it('lists the four values with a translated title and description', () => {
    expect(ORGANIZATION_VALUES.map((value) => value.id)).toEqual([
      'free',
      'inclusive',
      'community',
      'fun',
    ])
    for (const value of ORGANIZATION_VALUES) {
      expect(value.titleKey).toBe(`pages.about.organization.values.${value.id}.title`)
      expect(value.descriptionKey).toBe(`pages.about.organization.values.${value.id}.description`)
      expect(t(value.titleKey)).not.toBe(value.titleKey)
      expect(value.icon).not.toBe('')
      expect(value.soft).toMatch(/^rgba\(/)
    }
  })

  it('numbers the four activities in order with their colors', () => {
    expect(ORGANIZATION_ACTIVITIES.map((activity) => [activity.id, activity.number])).toEqual([
      ['workshops', '01'],
      ['annualDay', '02'],
      ['meetAndCode', '03'],
      ['competitions', '04'],
    ])
    for (const activity of ORGANIZATION_ACTIVITIES) {
      expect(activity.titleKey).toBe(`pages.about.organization.activities.${activity.id}.title`)
      expect(t(activity.descriptionKey)).not.toBe(activity.descriptionKey)
      expect(activity.color).toMatch(/^#[0-9A-F]{6}$/i)
    }
  })
})
