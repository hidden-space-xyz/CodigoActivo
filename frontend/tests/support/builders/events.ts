import type {
  EventCategoryResponse,
  EventListItemResponse,
  EventRatingListItemResponse,
  EventResponse,
  EventTermsDocumentResponse,
  EventTermsDocumentStateResponse,
  LeaderRosterActivityResponse,
} from '@/shared/api/generated/models'

import { AUDIT, FAR_FUTURE, LONG_AGO, richText } from './common'

/** Identifier of the default event. */
export const EVENT_ID = 'event-1'

/** Thumbnail of the default event and of its activities. */
export const THUMBNAIL_ID = 'thumb-event'

/** Category tag of the default event. */
export function buildEventCategory(
  overrides: Partial<EventCategoryResponse> = {},
): EventCategoryResponse {
  return { categoryTypeId: 'cat-1', name: 'Programación', color: '#ff6600', ...overrides }
}

/** Upcoming event list item whose general signup is open right now. */
export function buildEventListItem(
  overrides: Partial<EventListItemResponse> = {},
): EventListItemResponse {
  return {
    id: EVENT_ID,
    title: 'Hackathon de primavera',
    subtitle: 'Programa tu futuro',
    eventStartsAt: '2099-06-10',
    eventEndsAt: '2099-06-11',
    earlySignupStartsAt: null,
    signupStartsAt: LONG_AGO,
    signupEndsAt: FAR_FUTURE,
    ...AUDIT,
    updatedAt: null,
    thumbnailId: THUMBNAIL_ID,
    featured: false,
    categories: [buildEventCategory()],
    stage: 'SignupOpen',
    ...overrides,
  }
}

/** Terms document linked to the default event. */
export function buildEventTermsLink(
  overrides: Partial<EventTermsDocumentResponse> = {},
): EventTermsDocumentResponse {
  return {
    termsDocumentId: 'terms-1',
    name: 'Terms',
    required: true,
    displayOrder: 0,
    ...overrides,
  }
}

/** Full upcoming event whose general signup is open right now, without terms documents. */
export function buildEventResponse(overrides: Partial<EventResponse> = {}): EventResponse {
  return {
    ...buildEventListItem(),
    description: richText('Un fin de semana de código.'),
    termsDocuments: [],
    ...overrides,
  }
}

/** Anonymous rating of an event, as the admin opinions tab lists it. */
export function buildRating(
  overrides: Partial<EventRatingListItemResponse> = {},
): EventRatingListItemResponse {
  return {
    id: 'rating-1',
    score: 4,
    mostLiked: 'The people',
    leastLiked: '',
    suggestions: null,
    ...overrides,
  }
}

/** Required terms document served by `/api/events/:eventId/terms`, still undecided. */
export function buildTermsDocumentState(
  overrides: Partial<EventTermsDocumentStateResponse> = {},
): EventTermsDocumentStateResponse {
  return {
    termsDocumentId: 'terms-1',
    name: 'Normas del campamento',
    description: richText('Respeta a los demás.'),
    required: true,
    displayOrder: 0,
    accepted: null,
    decidedAt: null,
    ...overrides,
  }
}

/**
 * Led activity as returned by `/api/events/:eventId/leader-roster`: a co-leader and the signed-in
 * leader, a volunteer, and participants split into one adult and two dependents.
 */
export function buildLeaderRosterActivity(
  overrides: Partial<LeaderRosterActivityResponse> = {},
): LeaderRosterActivityResponse {
  return {
    activityId: 'act-1',
    title: 'Taller de robótica',
    location: 'Aula 3',
    activityStartsAt: '2099-06-10T09:00:00Z',
    activityEndsAt: '2099-06-10T11:00:00Z',
    roles: [
      {
        roleTypeId: 'role-leader',
        roleName: 'Líder',
        users: [
          {
            firstName: 'Luis',
            lastName: 'Lozano',
            email: 'luis@example.test',
            phone: '600 111 222',
            signedUpAt: '2099-05-01T10:00:00Z',
          },
          {
            firstName: 'Ada',
            lastName: 'Lovelace',
            email: 'ada@example.test',
            phone: '600 000 000',
            signedUpAt: '2099-05-02T10:00:00Z',
          },
        ],
        dependents: [],
      },
      {
        roleTypeId: 'role-volunteer',
        roleName: 'Voluntario',
        users: [
          {
            firstName: 'Víctor',
            lastName: 'Vega',
            email: '',
            phone: '',
            signedUpAt: '2099-05-03T10:00:00Z',
          },
        ],
        dependents: [],
      },
      {
        roleTypeId: 'role-participant',
        roleName: 'Participante',
        users: [
          {
            firstName: 'Ana',
            lastName: 'Álvarez',
            email: 'ana@example.test',
            phone: '600 333 444',
            signedUpAt: '2099-05-04T10:00:00Z',
          },
        ],
        dependents: [
          {
            firstName: 'Nora',
            lastName: 'Gil',
            age: 11,
            guardian: {
              firstName: 'Gabriela',
              lastName: 'Gil',
              email: 'gabriela@example.test',
              phone: '600 555 666',
            },
            signedUpAt: '2099-05-05T10:00:00Z',
          },
          {
            firstName: 'Hugo',
            lastName: 'Gil',
            age: 1,
            guardian: { firstName: 'Gabriela', lastName: 'Gil', email: '', phone: '' },
            signedUpAt: '2099-05-06T10:00:00Z',
          },
        ],
      },
    ],
    ...overrides,
  }
}
