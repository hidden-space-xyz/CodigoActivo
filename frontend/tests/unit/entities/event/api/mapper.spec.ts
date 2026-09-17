import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  toCategoryTag,
  toEventDetail,
  toPastEvent,
  toUpcomingEvent,
} from '@/entities/event/api/mapper'
import type { EventListItemResponse } from '@/shared/api/generated/models'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib'

import { t } from '../../../../support/render'

/** ISO timestamp for a local date-time relative to the frozen "now" (2026-09-17 12:00 local). */
function at(day: number, hour = 12): string {
  return new Date(2026, 8, day, hour, 0, 0).toISOString()
}

const NOW = new Date(2026, 8, 17, 12, 0, 0)

describe('event mapper', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(NOW)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('status', () => {
    const cases: [string, EventListItemResponse, string][] = [
      ['finished once the end date is before today', { eventEndsAt: '2026-09-16' }, 'finished'],
      [
        'finished even with an open signup window',
        { eventEndsAt: '2026-09-10T20:00:00', signupStartsAt: at(1), signupEndsAt: at(30) },
        'finished',
      ],
      ['upcoming without a signup window', { eventEndsAt: '2026-09-17' }, 'upcoming'],
      ['upcoming before the signup starts', { signupStartsAt: at(20) }, 'upcoming'],
      [
        'upcoming when the early signup has not started either',
        { signupStartsAt: at(20), earlySignupStartsAt: at(18) },
        'upcoming',
      ],
      [
        'earlySignupOpen once the early window started',
        { signupStartsAt: at(20), earlySignupStartsAt: at(16), signupEndsAt: at(25) },
        'earlySignupOpen',
      ],
      [
        'signupClosed after the signup ends',
        { signupStartsAt: at(1), signupEndsAt: at(16), eventEndsAt: '2026-10-01' },
        'signupClosed',
      ],
      ['signupClosed with only a past signup end', { signupEndsAt: at(17, 11) }, 'signupClosed'],
      [
        'signupOpen inside the signup window',
        { signupStartsAt: at(10), signupEndsAt: at(20) },
        'signupOpen',
      ],
      ['signupOpen with only a past signup start', { signupStartsAt: at(17, 11) }, 'signupOpen'],
      ['signupOpen with only a future signup end', { signupEndsAt: at(18) }, 'signupOpen'],
    ]

    it.each(cases)('is %s', (_name, event, kind) => {
      const status = toUpcomingEvent(event).status

      expect(status.kind).toBe(kind)
      expect(status.label).toBe(t(`entities.event.status.${kind}`))
    })

    it('treats a malformed end date as not finished', () => {
      expect(toUpcomingEvent({ eventEndsAt: 'not-a-date' }).status.kind).toBe('upcoming')
    })
  })

  it('maps an upcoming event card with a formatted date range and valid categories', () => {
    const event = toUpcomingEvent({
      id: 'event-1',
      title: 'Día Código Activo',
      subtitle: 'Programa tu futuro',
      eventStartsAt: '2026-10-03',
      eventEndsAt: '2026-10-04',
      thumbnailId: 'thumb-1',
      categories: [
        { categoryTypeId: 'cat-1', name: 'Robótica', color: '#ff0000' },
        { categoryTypeId: 'cat-2' },
        { name: 'Sin id', color: '#00ff00' },
      ],
    })

    expect(event).toEqual({
      id: 'event-1',
      title: 'Día Código Activo',
      slogan: 'Programa tu futuro',
      date: formatDateRange('2026-10-03', '2026-10-04'),
      status: { kind: 'upcoming', label: t('entities.event.status.upcoming') },
      thumbnailId: 'thumb-1',
      categories: [
        { id: 'cat-1', name: 'Robótica', color: '#ff0000' },
        { id: 'cat-2', name: '', color: '' },
      ],
    })
  })

  it('defaults a bare event card and shows the date fallback', () => {
    expect(toUpcomingEvent({})).toEqual({
      id: '',
      title: '',
      slogan: '',
      date: t('entities.event.dateFallback'),
      status: { kind: 'upcoming', label: t('entities.event.status.upcoming') },
      thumbnailId: '',
      categories: [],
    })
  })

  it('maps a category type to a tag with empty defaults', () => {
    expect(toCategoryTag({ id: 'cat-1', name: 'IA', color: '#123456' })).toEqual({
      id: 'cat-1',
      name: 'IA',
      color: '#123456',
    })
    expect(toCategoryTag({})).toEqual({ id: '', name: '', color: '' })
  })

  it('maps the detail with labels, open signup flag and terms', () => {
    const detail = toEventDetail({
      id: 'event-1',
      title: 'Día',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      eventStartsAt: '2026-10-03T09:00:00Z',
      eventEndsAt: '2026-10-03T18:00:00Z',
      signupStartsAt: at(10),
      signupEndsAt: at(30),
      thumbnailId: 'thumb-1',
      categories: [{ categoryTypeId: 'cat-1', name: 'IA', color: '#123456' }],
      termsDocument: { id: 'terms-1', name: 'Normas', description: 'Acepta' },
    })

    expect(detail).toEqual({
      id: 'event-1',
      title: 'Día',
      subtitle: 'Sub',
      description: '<p>Texto</p>',
      startsAt: '2026-10-03T09:00:00Z',
      endsAt: '2026-10-03T18:00:00Z',
      dateLabel: formatDateRange('2026-10-03T09:00:00Z', '2026-10-03T18:00:00Z'),
      signupLabel: formatDateTimeRange(at(10), at(30)),
      earlySignupLabel: null,
      status: { kind: 'signupOpen', label: t('entities.event.status.signupOpen') },
      thumbnailId: 'thumb-1',
      signupOpen: true,
      earlySignupOpen: false,
      categories: [{ id: 'cat-1', name: 'IA', color: '#123456' }],
      terms: { id: 'terms-1', name: 'Normas', description: 'Acepta' },
    })
  })

  it('flags the early signup window and formats its start', () => {
    const detail = toEventDetail({
      signupStartsAt: at(20),
      earlySignupStartsAt: at(15),
      termsDocument: { id: 'terms-1' },
    })

    expect(detail.earlySignupOpen).toBe(true)
    expect(detail.signupOpen).toBe(false)
    expect(detail.earlySignupLabel).toBe(formatDateTime(at(15)))
    expect(detail.terms).toEqual({ id: 'terms-1', name: '', description: '' })
  })

  it('defaults a bare detail and drops terms without an id', () => {
    expect(toEventDetail({ termsDocument: { name: 'Sin id' } })).toEqual({
      id: '',
      title: '',
      subtitle: '',
      description: '',
      startsAt: null,
      endsAt: null,
      dateLabel: t('entities.event.dateFallback'),
      signupLabel: '—',
      earlySignupLabel: null,
      status: { kind: 'upcoming', label: t('entities.event.status.upcoming') },
      thumbnailId: '',
      signupOpen: false,
      earlySignupOpen: false,
      categories: [],
      terms: null,
    })
  })

  it('maps past events as finished regardless of dates', () => {
    expect(
      toPastEvent({
        id: 'event-0',
        title: 'Edición 2025',
        subtitle: 'Crea',
        eventStartsAt: '2025-05-10',
        signupStartsAt: at(10),
        signupEndsAt: at(30),
        categories: [{ categoryTypeId: 'cat-1', name: 'IA', color: '#123456' }],
        thumbnailId: 'thumb-0',
      }),
    ).toEqual({
      id: 'event-0',
      title: 'Edición 2025',
      eventName: 'Crea',
      date: formatDateRange('2025-05-10'),
      status: { kind: 'finished', label: t('entities.event.status.finished') },
      thumbnailId: 'thumb-0',
      categories: [{ id: 'cat-1', name: 'IA', color: '#123456' }],
    })
    expect(toPastEvent({})).toMatchObject({ id: '', title: '', eventName: '', thumbnailId: '' })
  })
})
