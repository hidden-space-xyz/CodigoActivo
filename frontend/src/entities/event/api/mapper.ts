import type {
  CreateEventRequest,
  EventCategoryResponse,
  EventCategoryTypeResponse,
  EventListItemResponse,
  EventResponse,
  EventStage,
  EventTermsDocumentResponse,
  EventTermsDocumentStateResponse,
  EventTermsStateResponse,
  LeaderRosterActivityResponse,
  LeaderRosterDependentResponse,
  LeaderRosterUserResponse,
} from '@/shared/api/generated/models'

import type {
  EventCategoryTag,
  EventDetail,
  EventInput,
  EventListing,
  EventStatusKind,
  EventSummary,
  EventTermsDocumentState,
  EventTermsState,
  EventTermsSummary,
  LeaderRosterActivity,
  LeaderRosterDependent,
  LeaderRosterUser,
} from '../model/types'

const STAGE_KINDS: Record<EventStage, EventStatusKind> = {
  Upcoming: 'upcoming',
  EarlySignupOpen: 'earlySignupOpen',
  SignupOpen: 'signupOpen',
  SignupClosed: 'signupClosed',
  Finished: 'finished',
}

function toEventCategoryTag(category: EventCategoryResponse): EventCategoryTag {
  return { id: category.categoryTypeId, name: category.name, color: category.color }
}

/** Maps a category type to a tag. */
export function toCategoryTag(categoryType: EventCategoryTypeResponse): EventCategoryTag {
  return { id: categoryType.id, name: categoryType.name, color: categoryType.color }
}

/** Maps a list item to a card model; the status is the stage the API gave the event. */
export function toEventSummary(event: EventListItemResponse): EventSummary {
  return {
    id: event.id,
    title: event.title,
    subtitle: event.subtitle,
    startsAt: event.eventStartsAt,
    endsAt: event.eventEndsAt,
    status: STAGE_KINDS[event.stage],
    thumbnailId: event.thumbnailId,
    categories: event.categories.map(toEventCategoryTag),
  }
}

/** Maps a list item of the past events; a past event always shows as finished. */
export function toPastEventSummary(event: EventListItemResponse): EventSummary {
  return { ...toEventSummary(event), status: 'finished' }
}

/** Maps a list item for the admin events table. */
export function toEventListing(event: EventListItemResponse): EventListing {
  return {
    ...toEventSummary(event),
    featured: event.featured,
    signupStartsAt: event.signupStartsAt,
    signupEndsAt: event.signupEndsAt,
    earlySignupStartsAt: event.earlySignupStartsAt ?? null,
  }
}

/**
 * Builds the body that creates or replaces an event; both endpoints take the same fields and an
 * event without terms documents sends none.
 */
export function toEventRequest(input: EventInput): CreateEventRequest {
  return {
    title: input.title,
    subtitle: input.subtitle,
    description: input.description,
    eventStartsAt: input.startsAt,
    eventEndsAt: input.endsAt,
    earlySignupStartsAt: input.earlySignupStartsAt,
    signupStartsAt: input.signupStartsAt,
    signupEndsAt: input.signupEndsAt,
    thumbnailId: input.thumbnailId,
    categoryTypeIds: [...input.categoryIds],
    termsDocuments: input.terms.length
      ? input.terms.map((terms) => ({
          termsDocumentId: terms.documentId,
          required: terms.required,
        }))
      : null,
  }
}

function toTermsSummary(document: EventTermsDocumentResponse): EventTermsSummary {
  return {
    id: document.termsDocumentId,
    name: document.name,
    required: document.required,
    displayOrder: document.displayOrder,
  }
}

/** Maps a whole event for its public page. */
export function toEventDetail(event: EventResponse): EventDetail {
  return {
    id: event.id,
    title: event.title,
    subtitle: event.subtitle,
    description: event.description,
    startsAt: event.eventStartsAt,
    endsAt: event.eventEndsAt,
    signupStartsAt: event.signupStartsAt,
    signupEndsAt: event.signupEndsAt,
    earlySignupStartsAt: event.earlySignupStartsAt ?? null,
    status: STAGE_KINDS[event.stage],
    thumbnailId: event.thumbnailId,
    categories: event.categories.map(toEventCategoryTag),
    terms: event.termsDocuments.map(toTermsSummary),
  }
}

function toTermsDocumentState(document: EventTermsDocumentStateResponse): EventTermsDocumentState {
  return {
    id: document.termsDocumentId,
    name: document.name,
    description: document.description,
    required: document.required,
    displayOrder: document.displayOrder,
    accepted: document.accepted ?? null,
    decidedAt: document.decidedAt ?? null,
  }
}

/** Maps the event's terms state for the signed-in user, sorted by the event's display order. */
export function toEventTermsState(response: EventTermsStateResponse): EventTermsState {
  return {
    documents: response.documents
      .map(toTermsDocumentState)
      .sort((a, b) => a.displayOrder - b.displayOrder),
    signupBlocked: response.signupBlocked,
  }
}

function toLeaderRosterUser(user: LeaderRosterUserResponse): LeaderRosterUser {
  return {
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email ?? '',
    phone: user.phone ?? '',
    signedUpAt: user.signedUpAt,
  }
}

function toLeaderRosterDependent(dependent: LeaderRosterDependentResponse): LeaderRosterDependent {
  return {
    firstName: dependent.firstName,
    lastName: dependent.lastName,
    age: dependent.age ?? null,
    guardian: {
      firstName: dependent.guardian.firstName,
      lastName: dependent.guardian.lastName,
      email: dependent.guardian.email ?? '',
      phone: dependent.guardian.phone ?? '',
    },
    signedUpAt: dependent.signedUpAt,
  }
}

/** Maps one led activity with its confirmed attendees, keeping the API's role and name order. */
export function toLeaderRosterActivity(
  activity: LeaderRosterActivityResponse,
): LeaderRosterActivity {
  return {
    id: activity.activityId,
    title: activity.title,
    location: activity.location,
    startsAt: activity.activityStartsAt,
    endsAt: activity.activityEndsAt,
    roles: activity.roles.map((role) => ({
      id: role.roleTypeId,
      name: role.roleName,
      users: role.users.map(toLeaderRosterUser),
      dependents: role.dependents.map(toLeaderRosterDependent),
    })),
  }
}
