import { getGetApiEventsEventIdTermsDecisionsUserIdTermsDocumentIdSignatureUrl } from '@/shared/api/generated/endpoints/events/events'
import type {
  EventAttendeeAssignmentResponse,
  EventAttendeeResponse,
  EventRatingListItemResponse,
  EventSummaryResponse,
  EventTermsDecisionResponse,
} from '@/shared/api/generated/models'

import type {
  AttendeeAssignment,
  EventAttendee,
  EventRating,
  EventStats,
  EventTermsDecision,
} from '../model/types'

/** Maps the report summary of an event to its summary cards. */
export function toEventStats(summary: EventSummaryResponse): EventStats {
  return {
    activitiesCount: summary.activitiesCount,
    roles: summary.roleTypeBreakdown.map((role) => ({
      name: role.roleTypeName ?? '',
      approved: role.approvedAssignments,
    })),
    scoredRatingsCount: summary.scoredRatingsCount,
    ratingsAverage: summary.ratingsAverage ?? null,
  }
}

function toAttendeeAssignment(assignment: EventAttendeeAssignmentResponse): AttendeeAssignment {
  return {
    activityId: assignment.activityId,
    activityTitle: assignment.activityTitle,
    roleTypeId: assignment.roleTypeId,
    roleName: assignment.roleTypeName ?? '',
    statusId: assignment.statusId,
    statusName: assignment.statusName ?? '',
    signedUpAt: assignment.signedUpAt,
    hasTimeConflict: assignment.hasTimeConflict,
  }
}

/** Maps an attendee of the event attendees report. */
export function toEventAttendee(attendee: EventAttendeeResponse): EventAttendee {
  const guardian = attendee.guardian
  return {
    userId: attendee.userId,
    firstName: attendee.firstName ?? '',
    lastName: attendee.lastName ?? '',
    email: attendee.email ?? '',
    phone: attendee.phone ?? '',
    secondaryPhone: attendee.secondaryPhone ?? '',
    birthDate: attendee.birthDate ?? null,
    gender: attendee.gender,
    userTypeName: attendee.userTypeName,
    userTypeColor: attendee.userTypeColor,
    guardian: guardian
      ? {
          firstName: guardian.firstName,
          lastName: guardian.lastName,
          email: guardian.email ?? '',
          phone: guardian.phone ?? '',
          secondaryPhone: guardian.secondaryPhone ?? '',
        }
      : null,
    assignments: attendee.assignments.map(toAttendeeAssignment),
  }
}

/** Maps an anonymous rating of the event. */
export function toEventRating(rating: EventRatingListItemResponse): EventRating {
  return {
    id: rating.id,
    score: rating.score ?? null,
    mostLiked: rating.mostLiked ?? '',
    leastLiked: rating.leastLiked ?? '',
    suggestions: rating.suggestions ?? '',
  }
}

/** Maps a terms decision of the event, pointing at its signature image when it has one. */
export function toEventTermsDecision(
  eventId: string,
  decision: EventTermsDecisionResponse,
): EventTermsDecision {
  return {
    userId: decision.userId,
    firstName: decision.firstName,
    lastName: decision.lastName,
    email: decision.email ?? '',
    decidedById: decision.decidedById,
    decidedByName: `${decision.decidedByFirstName} ${decision.decidedByLastName}`,
    termsDocumentId: decision.termsDocumentId,
    documentName: decision.documentName,
    consent: decision.documentKind === 'Consent',
    accepted: decision.accepted,
    withdrawn: decision.withdrawn,
    decidedAt: decision.decidedAt,
    signatureUrl: decision.hasSignature
      ? getGetApiEventsEventIdTermsDecisionsUserIdTermsDocumentIdSignatureUrl(
          eventId,
          decision.userId,
          decision.termsDocumentId,
        )
      : '',
  }
}
