import { describe, expect, it } from 'vitest'

import {
  contactLines,
  guardianContactLines,
  roleHeadingKey,
  rosterRows,
} from '@/pages/admin/event-roster/lib/roster-rows'
import type { RosterActivity, RosterParticipant } from '@/pages/admin/event-roster/model/types'

function participant(overrides: Partial<RosterParticipant> = {}): RosterParticipant {
  return {
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    birthDate: null,
    email: 'ada@example.test',
    phone: '600000001',
    secondaryPhone: '',
    roleName: 'Monitora',
    guardian: null,
    ...overrides,
  }
}

function activity(participants: RosterParticipant[]): RosterActivity {
  return {
    id: 'act-1',
    title: 'Robotics',
    location: 'Room 1',
    startsAt: '2026-10-10T09:00:00Z',
    endsAt: '2026-10-10T11:00:00Z',
    participants,
  }
}

describe('rosterRows', () => {
  it('opens each run of the same role with its heading', () => {
    const rows = rosterRows(
      activity([
        participant({ userId: 'a' }),
        participant({ userId: 'b' }),
        participant({ userId: 'c', roleName: 'Tutor' }),
      ]),
    )

    expect(rows.map((row) => row.key)).toEqual(['role:Monitora', 'p:a', 'p:b', 'role:Tutor', 'p:c'])
    expect(rows[0]).toEqual({ kind: 'role', key: 'role:Monitora', roleName: 'Monitora' })
  })

  it('has no rows without participants', () => {
    expect(rosterRows(activity([]))).toEqual([])
  })
})

describe('roleHeadingKey', () => {
  it('pluralizes role names ending in a vowel or a consonant', () => {
    expect(roleHeadingKey('Monitora')).toBe('pages.admin.eventRoster.rolePluralVowel')
    expect(roleHeadingKey('Monitor')).toBe('pages.admin.eventRoster.rolePluralConsonant')
    expect(roleHeadingKey('Participanté')).toBe('pages.admin.eventRoster.rolePluralVowel')
  })
})

describe('contactLines and guardianContactLines', () => {
  it('list the known ways to reach a participant, phones first', () => {
    expect(contactLines(participant({ secondaryPhone: '600000011' }))).toEqual([
      '600000001',
      '600000011',
      'ada@example.test',
    ])
    expect(contactLines(participant({ email: '', phone: '' }))).toEqual([])
  })

  it('list the name and ways to reach the guardian of a minor', () => {
    expect(
      guardianContactLines(
        participant({
          guardian: {
            firstName: 'Mary',
            lastName: '',
            email: '',
            phone: '611000000',
            secondaryPhone: '611000011',
          },
        }),
      ),
    ).toEqual(['Mary', '611000000', '611000011'])
    expect(guardianContactLines(participant())).toEqual([])
  })
})
