import { describe, expect, it } from 'vitest'

import {
  accentColor,
  hiddenActivityCount,
  toSheets,
  visibleActivities,
} from '@/pages/admin/event-badges/lib/badges'
import type { Badge } from '@/pages/admin/event-badges/model/types'

function badge(overrides: Partial<Badge> = {}): Badge {
  return {
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    userTypeName: 'Member',
    userTypeColor: '#123456',
    guardian: null,
    activities: [],
    ...overrides,
  }
}

describe('toSheets', () => {
  it('fills sheets of twelve badges in order', () => {
    const badges = Array.from({ length: 13 }, (_, index) => badge({ userId: `user-${index}` }))

    const sheets = toSheets(badges)

    expect(sheets.map((sheet) => sheet.length)).toEqual([12, 1])
    expect(sheets[1]?.[0]?.userId).toBe('user-12')
    expect(toSheets([])).toEqual([])
  })
})

describe('accentColor', () => {
  it('uses the user type color unless it is missing or too light to read', () => {
    expect(accentColor(badge({ userTypeColor: '#123456' }))).toBe('#123456')
    expect(accentColor(badge({ userTypeColor: '#fafafa' }))).toBe('#475569')
    expect(accentColor(badge({ userTypeColor: 'not-a-color' }))).toBe('#475569')
    expect(accentColor(badge({ userTypeColor: '' }))).toBe('#475569')
  })
})

describe('visibleActivities and hiddenActivityCount', () => {
  it('list six activities and count the rest', () => {
    const activities = Array.from({ length: 8 }, (_, index) => ({
      title: `A${index + 1}`,
      location: 'Aula',
    }))

    expect(visibleActivities(badge({ activities })).map((activity) => activity.title)).toEqual([
      'A1',
      'A2',
      'A3',
      'A4',
      'A5',
      'A6',
    ])
    expect(hiddenActivityCount(badge({ activities }))).toBe(2)
    expect(hiddenActivityCount(badge({ activities: activities.slice(0, 3) }))).toBe(0)
  })
})
