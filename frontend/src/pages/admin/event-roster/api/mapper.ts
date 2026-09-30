import type {
  EventRosterParticipantResponse,
  EventRosterResponse,
} from '@/shared/api/generated/models'

import type { EventRoster, RosterParticipant } from '../model/types'

function toRosterParticipant(participant: EventRosterParticipantResponse): RosterParticipant {
  const guardian = participant.guardian
  return {
    userId: participant.userId,
    firstName: participant.firstName,
    lastName: participant.lastName,
    birthDate: participant.birthDate ?? null,
    email: participant.email ?? '',
    phone: participant.phone ?? '',
    secondaryPhone: participant.secondaryPhone ?? '',
    roleName: participant.roleName,
    guardian: guardian
      ? {
          firstName: guardian.firstName,
          lastName: guardian.lastName,
          email: guardian.email ?? '',
          phone: guardian.phone ?? '',
          secondaryPhone: guardian.secondaryPhone ?? '',
        }
      : null,
  }
}

/** Maps the attendance roster report of an event. */
export function toEventRoster(report: EventRosterResponse): EventRoster {
  return {
    title: report.title,
    activities: report.activities.map((activity) => ({
      id: activity.activityId,
      title: activity.title,
      location: activity.location,
      startsAt: activity.activityStartsAt,
      endsAt: activity.activityEndsAt,
      participants: activity.participants.map(toRosterParticipant),
    })),
  }
}
