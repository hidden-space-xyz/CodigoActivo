import { describe, expect, it } from 'vitest'

import { announcementQueryKeys } from '@/entities/announcement'

describe('announcementQueryKeys', () => {
  it('nests every announcement key under the shared root', () => {
    expect(announcementQueryKeys.all).toEqual(['announcements'])
    expect(announcementQueryKeys.publicDetail('a1')).toEqual(['announcements', 'public', 'a1'])
    expect(announcementQueryKeys.years()).toEqual(['announcements', 'years'])
    expect(announcementQueryKeys.byYear('2026', 'robot')).toEqual([
      'announcements',
      'year',
      '2026',
      'robot',
    ])
    expect(announcementQueryKeys.home()).toEqual(['announcements', 'home'])
  })
})
