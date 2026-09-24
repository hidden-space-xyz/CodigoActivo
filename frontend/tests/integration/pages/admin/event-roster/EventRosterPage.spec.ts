import { flushPromises } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { EventRosterPage } from '@/pages/admin/event-roster'
import type {
  EventRosterActivityResponse,
  EventRosterResponse,
} from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'
import { ageFrom, formatDateTimeRange } from '@/shared/lib'

import {
  buildRosterActivity,
  buildRosterParticipant,
  EVENT_ID,
  stubDocumentFonts,
} from '../../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

let restoreFonts: () => void = () => undefined

beforeEach(() => {
  restoreFonts = stubDocumentFonts()
})

afterEach(() => {
  restoreFonts()
})

function participantsLabel(count: number): string {
  return (i18n.global.t as (key: string, values: object, plural: number) => string)(
    'pages.admin.eventRoster.participantsCount',
    { count },
    count,
  )
}

/**
 * Layout in millimetres (1 px per mm): sheet header 10, activity header 20 (200 for the third
 * activity), table header 10, role rows 15 and participant rows 30, so a sheet holds 265 mm.
 */
function mockLayout(): void {
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    let width = 0
    let height = 0
    if (this.classList.contains('measure')) width = 210
    else if (this.getAttribute('data-part') === 'sheet-head') height = 10
    else if (this.getAttribute('data-part') === 'head') {
      height = this.closest('[data-activity-index="2"]') ? 200 : 20
    } else if (this.tagName === 'THEAD') height = 10
    else if (this.tagName === 'TR' && this.closest('tbody')) {
      height = this.classList.contains('list__role-row') ? 15 : 30
    }
    return new DOMRect(0, 0, width, height)
  })
}

async function renderRoster(activities: EventRosterActivityResponse[], title = 'Hackathon') {
  server.use(
    http.get('/api/reports/events/:eventId/roster', () =>
      HttpResponse.json({ eventId: EVENT_ID, title, activities } satisfies EventRosterResponse),
    ),
  )
  const rendered = await renderWithProviders(EventRosterPage, {
    route: `/admin/events/${EVENT_ID}/roster`,
    attach: true,
  })
  await flushPromises()
  return rendered
}

function sheets() {
  return [...document.body.querySelectorAll('.sheet:not(.measure)')]
}

function chunkSummary() {
  return sheets().map((sheet) =>
    [...sheet.querySelectorAll('section.chunk')].map((chunk) => ({
      title: chunk.querySelector('.chunk__title')?.textContent?.trim(),
      rows: [...chunk.querySelectorAll('tbody tr')].map((row) =>
        row.classList.contains('list__role-row')
          ? `[${row.textContent?.trim()}]`
          : (row.querySelector('.list__name')?.textContent?.trim() ?? ''),
      ),
    })),
  )
}

const people = (prefix: string, count: number, roleName: string | null) =>
  Array.from({ length: count }, (_, index) =>
    buildRosterParticipant({
      userId: `${prefix}-${index + 1}`,
      firstName: `${prefix}${index + 1}`,
      lastName: null,
      roleName,
    }),
  )

describe('EventRosterPage', () => {
  it('prints participant details grouped by role', async () => {
    mockLayout()
    const { wrapper } = await renderRoster([
      buildRosterActivity({
        participants: [
          buildRosterParticipant({
            secondaryPhone: '600000011',
            guardian: {
              firstName: 'Mary',
              lastName: null,
              phone: '611000000',
              secondaryPhone: '611000011',
              email: null,
            },
          }),
          buildRosterParticipant({
            userId: 'user-2',
            firstName: 'Tim',
            lastName: 'Berners',
            birthDate: '',
            email: null,
            phone: null,
            roleName: 'Tutor',
          }),
          buildRosterParticipant({
            userId: 'user-3',
            firstName: 'Grace',
            lastName: 'Hopper',
            roleName: 'Tutor',
            guardian: {},
          }),
        ],
      }),
      buildRosterActivity({
        activityId: 'act-2',
        title: 'Painting',
        location: null,
        participants: null,
      }),
    ])

    await vi.waitFor(() => expect(sheets()).toHaveLength(1))
    const sheet = sheets()[0]
    expect(sheet?.querySelector('.sheet__event')?.textContent?.trim()).toBe(
      t('pages.admin.eventRoster.sheetHeader', { title: 'Hackathon' }),
    )
    expect(wrapper.find('.back').attributes('href')).toBe(`/admin/events/${EVENT_ID}`)

    const [robotics, painting] = [...(sheet?.querySelectorAll('section.chunk') ?? [])]
    const meta = robotics?.querySelector('.chunk__meta')?.textContent ?? ''
    expect(meta).toContain(formatDateTimeRange('2026-10-10T09:00:00Z', '2026-10-10T11:00:00Z'))
    expect(meta).toContain('Room 1')
    expect(meta).toContain(participantsLabel(3))
    // Activities without participants have no rows, so they are measured but not printed.
    expect(painting).toBeUndefined()
    const measured = document.body.querySelector('.measure [data-activity-index="1"] .chunk__meta')
    expect(measured?.querySelectorAll('span')).toHaveLength(2)
    expect(measured?.textContent).toContain(participantsLabel(0))

    const cellText = (cell: Element) => {
      const lines = [...cell.querySelectorAll('div')].map((line) => line.textContent?.trim())
      return lines.length > 0 ? lines.join('|') : (cell.textContent?.trim() ?? '')
    }
    const rows = [...(robotics?.querySelectorAll('tbody tr') ?? [])].map((row) =>
      [...row.querySelectorAll('td')].map(cellText),
    )
    const age = t('pages.admin.eventRoster.ageYears', { age: ageFrom('2000-01-01') })
    expect(rows).toEqual([
      [t('pages.admin.eventRoster.rolePluralVowel', { name: 'Monitora' })],
      ['', 'Ada Lovelace', age, '600000001|600000011|ada@example.test', 'Mary|611000000|611000011'],
      [t('pages.admin.eventRoster.rolePluralConsonant', { name: 'Tutor' })],
      ['', 'Tim Berners', '—', '—', '—'],
      ['', 'Grace Hopper', age, '600000001|ada@example.test', '—'],
    ])
  })

  it('uses a generic heading for participants without role', async () => {
    mockLayout()
    await renderRoster([buildRosterActivity({ participants: people('P', 1, null) })])

    await vi.waitFor(() => expect(sheets()).toHaveLength(1))
    expect(chunkSummary()).toEqual([
      [{ title: 'Robotics', rows: [`[${t('pages.admin.eventRoster.roleFallback')}]`, 'P1'] }],
    ])
  })

  it('splits activities across A4 sheets, repeating headers on continued chunks', async () => {
    mockLayout()
    await renderRoster([
      buildRosterActivity({
        activityId: 'act-1',
        title: 'First',
        participants: [...people('M', 6, 'Monitora'), ...people('V', 5, 'Voluntaria')],
      }),
      buildRosterActivity({
        activityId: 'act-2',
        title: 'Second',
        location: null,
        participants: people('S', 3, ''),
      }),
      buildRosterActivity({
        activityId: 'act-3',
        title: 'Third',
        participants: people('T', 1, 'Monitora'),
      }),
      buildRosterActivity({
        activityId: 'act-4',
        title: 'Fourth',
        participants: people('F', 8, 'Monitora'),
      }),
      buildRosterActivity({
        activityId: 'act-5',
        title: 'Fifth',
        participants: people('E', 1, 'Monitora'),
      }),
    ])

    await vi.waitFor(() => expect(sheets()).toHaveLength(6))
    const continued = t('pages.admin.eventRoster.continued')
    const monitoras = `[${t('pages.admin.eventRoster.rolePluralVowel', { name: 'Monitora' })}]`
    const voluntarias = `[${t('pages.admin.eventRoster.rolePluralVowel', { name: 'Voluntaria' })}]`
    const fallback = `[${t('pages.admin.eventRoster.roleFallback')}]`
    expect(chunkSummary()).toEqual([
      [{ title: 'First', rows: [monitoras, 'M1', 'M2', 'M3', 'M4', 'M5', 'M6'] }],
      [{ title: `First${continued}`, rows: [voluntarias, 'V1', 'V2', 'V3', 'V4', 'V5'] }],
      [{ title: 'Second', rows: [fallback, 'S1', 'S2', 'S3'] }],
      [{ title: 'Third', rows: [monitoras, 'T1'] }],
      [{ title: 'Fourth', rows: [monitoras, 'F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7'] }],
      [
        { title: `Fourth${continued}`, rows: [monitoras, 'F8'] },
        { title: 'Fifth', rows: [monitoras, 'E1'] },
      ],
    ])
    const second = sheets()[2]?.querySelector('.chunk__meta')
    expect(second?.querySelectorAll('span')).toHaveLength(2)
  })

  it('adds the A4 print rule while mounted and prints on demand', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined)
    const { wrapper } = await renderRoster([buildRosterActivity()])
    const pageRule = () =>
      [...document.head.querySelectorAll('style')].some((style) =>
        style.textContent?.includes('@page { size: A4 portrait; margin: 0; }'),
      )
    expect(pageRule()).toBe(true)

    await wrapper.find('.print-btn').trigger('click')
    expect(print).toHaveBeenCalledTimes(1)

    wrapper.unmount()
    expect(pageRule()).toBe(false)
  })

  it('shows the empty and error states', async () => {
    const empty = await renderRoster([])
    await vi.waitFor(() =>
      expect(empty.wrapper.text()).toContain(t('pages.admin.eventRoster.emptyText')),
    )
    expect(sheets()).toHaveLength(0)
    empty.wrapper.unmount()

    server.use(http.get('/api/reports/events/:eventId/roster', () => apiError(500)))
    const failing = await renderWithProviders(EventRosterPage, {
      route: `/admin/events/${EVENT_ID}/roster`,
    })
    await vi.waitFor(() => expect(failing.wrapper.text()).toContain(t('dataState.error')))
  })

  it('drops the pagination when unmounted before fonts are ready', async () => {
    let releaseFonts: () => void = () => undefined
    Object.defineProperty(document, 'fonts', {
      configurable: true,
      value: {
        ready: new Promise<void>((resolve) => {
          releaseFonts = resolve
        }),
      },
    })
    server.use(
      http.get('/api/reports/events/:eventId/roster', () =>
        HttpResponse.json({ eventId: EVENT_ID, title: 'Hackathon', activities: [] }),
      ),
    )
    const { wrapper } = await renderWithProviders(EventRosterPage, {
      route: `/admin/events/${EVENT_ID}/roster`,
    })

    wrapper.unmount()
    releaseFonts()
    await flushPromises()

    expect(document.body.querySelectorAll('.sheet')).toHaveLength(0)
  })
})
