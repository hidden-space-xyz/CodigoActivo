import { describe, expect, it } from 'vitest'

import { eventQueryKeys, eventReportQueryKeys } from '@/entities/event'

describe('eventQueryKeys', () => {
  it('nests every event key under the events root', () => {
    expect(eventQueryKeys.all).toEqual(['events'])
    expect(eventQueryKeys.upcoming()).toEqual(['events', 'upcoming'])
    expect(eventQueryKeys.board()).toEqual(['events', 'board'])
    expect(eventQueryKeys.pastYears()).toEqual(['events', 'past-years'])
    expect(eventQueryKeys.pastCategories()).toEqual(['events', 'past-categories'])
    expect(eventQueryKeys.past('2025', 'robot', 'cat-1')).toEqual([
      'events',
      'past',
      '2025',
      'robot',
      'cat-1',
    ])
    expect(eventQueryKeys.detail('e1')).toEqual(['events', 'detail', 'e1'])
    expect(eventQueryKeys.termsAcceptance('e1')).toEqual(['events', 'terms-acceptance', 'e1'])
    expect(eventQueryKeys.adminTable()).toEqual(['events', 'admin'])
    expect(eventQueryKeys.adminDetail('e1')).toEqual(['events', 'admin-detail', 'e1'])
    expect(eventQueryKeys.ratings()).toEqual(['events', 'ratings'])
  })
})

describe('eventReportQueryKeys', () => {
  it('keeps report keys under a separate reports root', () => {
    expect(eventReportQueryKeys.all).toEqual(['reports'])
    expect(eventReportQueryKeys.summary('e1')).toEqual(['reports', 'event-summary', 'e1'])
    expect(eventReportQueryKeys.attendees()).toEqual(['reports', 'event-attendees'])
    expect(eventReportQueryKeys.badges('e1')).toEqual(['reports', 'event-badges', 'e1'])
    expect(eventReportQueryKeys.roster('e1')).toEqual(['reports', 'event-roster', 'e1'])
    expect(eventReportQueryKeys.dashboardAnalytics('2026-01-01', '2026-12-31')).toEqual([
      'reports',
      'dashboard-analytics',
      '2026-01-01',
      '2026-12-31',
    ])
  })
})
