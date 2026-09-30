import type { TranslationKey } from '@/shared/i18n'

import type { RosterActivity, RosterParticipant } from '../model/types'

/** Heading row that opens the participants of one role. */
export interface RoleRow {
  readonly kind: 'role'
  readonly key: string
  readonly roleName: string
}

/** Row of one participant. */
interface ParticipantRow {
  readonly kind: 'participant'
  readonly key: string
  readonly participant: RosterParticipant
}

/** Row of a roster table. */
export type RosterRow = RoleRow | ParticipantRow

/** Rows of an activity's table: its participants, with a heading each time the role changes. */
export function rosterRows(activity: RosterActivity): RosterRow[] {
  const rows: RosterRow[] = []
  let currentRole: string | null = null
  for (const participant of activity.participants) {
    if (participant.roleName !== currentRole) {
      currentRole = participant.roleName
      rows.push({ kind: 'role', key: `role:${currentRole}`, roleName: currentRole })
    }
    rows.push({ kind: 'participant', key: `p:${participant.userId}`, participant })
  }
  return rows
}

/** Translation key of a role heading, which pluralizes the role name as Spanish does. */
export function roleHeadingKey(roleName: string): TranslationKey {
  return /[aeiouáéíóú]$/i.test(roleName)
    ? 'pages.admin.eventRoster.rolePluralVowel'
    : 'pages.admin.eventRoster.rolePluralConsonant'
}

/** Ways to reach a participant, phones first. */
export function contactLines(participant: RosterParticipant): string[] {
  return [participant.phone, participant.secondaryPhone, participant.email].filter(Boolean)
}

/** Name and ways to reach the guardian of a minor; none for an adult. */
export function guardianContactLines(participant: RosterParticipant): string[] {
  const guardian = participant.guardian
  if (!guardian) return []
  const name = [guardian.firstName, guardian.lastName].filter(Boolean).join(' ')
  return [name, guardian.phone, guardian.secondaryPhone, guardian.email].filter(Boolean)
}
