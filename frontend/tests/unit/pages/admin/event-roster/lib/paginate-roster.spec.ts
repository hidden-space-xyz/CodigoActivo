import { describe, expect, it } from 'vitest'

import { paginateRoster, type RosterLayout } from '@/pages/admin/event-roster/lib/paginate-roster'
import { rosterRows } from '@/pages/admin/event-roster/lib/roster-rows'
import type { RosterActivity } from '@/pages/admin/event-roster/model/types'

const ROLE_ROW = 15
const PARTICIPANT_ROW = 30

function activity(id: string, roles: [string, number][]): RosterActivity {
  return {
    id,
    title: id,
    location: 'Room',
    startsAt: '2026-10-10T09:00:00Z',
    endsAt: '2026-10-10T11:00:00Z',
    participants: roles.flatMap(([roleName, count]) =>
      Array.from({ length: count }, (_, index) => ({
        userId: `${id}-${roleName}-${String(index + 1)}`,
        firstName: `${roleName}${String(index + 1)}`,
        lastName: '',
        birthDate: null,
        email: '',
        phone: '',
        secondaryPhone: '',
        roleName,
        guardian: null,
      })),
    ),
  }
}

function layoutOf(activities: RosterActivity[], headHeights: number[] = []): RosterLayout {
  return {
    capacity: 265,
    gap: 5,
    activities: activities.map((item, index) => ({
      headHeight: headHeights[index] ?? 20,
      theadHeight: 10,
      rowHeights: rosterRows(item).map((row) => (row.kind === 'role' ? ROLE_ROW : PARTICIPANT_ROW)),
    })),
  }
}

function summary(sheets: ReturnType<typeof paginateRoster>) {
  return sheets.map((sheet) =>
    sheet.map((chunk) => ({
      id: chunk.activity.id,
      continued: chunk.continued,
      rows: chunk.rows.map((row) =>
        row.kind === 'role' ? `[${row.roleName}]` : row.participant.firstName,
      ),
    })),
  )
}

describe('paginateRoster', () => {
  it('keeps short activities together on one sheet', () => {
    const activities = [activity('a', [['M', 2]]), activity('b', [['T', 1]])]

    expect(summary(paginateRoster(activities, layoutOf(activities)))).toEqual([
      [
        { id: 'a', continued: false, rows: ['[M]', 'M1', 'M2'] },
        { id: 'b', continued: false, rows: ['[T]', 'T1'] },
      ],
    ])
  })

  it('continues a long table on the next sheet, repeating the heading of its role', () => {
    const activities = [activity('a', [['M', 9]])]

    expect(summary(paginateRoster(activities, layoutOf(activities)))).toEqual([
      [{ id: 'a', continued: false, rows: ['[M]', 'M1', 'M2', 'M3', 'M4', 'M5', 'M6', 'M7'] }],
      [{ id: 'a', continued: true, rows: ['[M]', 'M8', 'M9'] }],
    ])
  })

  it('never leaves a role heading without its first participant', () => {
    const activities = [
      activity('a', [
        ['M', 6],
        ['V', 2],
      ]),
    ]

    expect(summary(paginateRoster(activities, layoutOf(activities)))).toEqual([
      [{ id: 'a', continued: false, rows: ['[M]', 'M1', 'M2', 'M3', 'M4', 'M5', 'M6'] }],
      [{ id: 'a', continued: true, rows: ['[V]', 'V1', 'V2'] }],
    ])
  })

  it('starts an activity on a new sheet when its header and first row do not fit', () => {
    const activities = [activity('a', [['M', 6]]), activity('b', [['T', 1]])]

    expect(
      summary(paginateRoster(activities, layoutOf(activities, [20, 60]))).map((sheet) =>
        sheet.map((chunk) => chunk.id),
      ),
    ).toEqual([['a'], ['b']])
  })

  it('leaves out activities without participants or measures', () => {
    const activities = [activity('a', []), activity('b', [['T', 1]])]

    expect(paginateRoster(activities, layoutOf(activities))).toHaveLength(1)
    expect(
      paginateRoster(activities, { ...layoutOf(activities), activities: [null, null] }),
    ).toEqual([])
  })
})
