import type {
  DashboardAnalyticsResponse,
  EventAttendeeAssignmentResponse,
  EventAttendeeResponse,
  EventBadgeResponse,
  EventRosterActivityResponse,
  EventRosterParticipantResponse,
  EventSummaryResponse,
} from '@/shared/api/generated/models'

import { EVENT_ID } from './events'

/** Pending signup of the default attendee to the default activity. */
export function buildAttendeeAssignment(
  overrides: Partial<EventAttendeeAssignmentResponse> = {},
): EventAttendeeAssignmentResponse {
  return {
    activityId: 'act-1',
    activityTitle: 'Robotics',
    activityStartsAt: '2026-10-10T09:00:00.000Z',
    activityEndsAt: '2026-10-10T11:00:00.000Z',
    roleTypeId: 'role-1',
    roleTypeName: 'Volunteer',
    statusId: 'status-1',
    statusName: 'Requested',
    signedUpAt: '2026-09-02T10:00:00Z',
    hasTimeConflict: false,
    ...overrides,
  }
}

/** Adult attendee of the default event with one signup. */
export function buildAttendee(
  overrides: Partial<EventAttendeeResponse> = {},
): EventAttendeeResponse {
  return {
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000001',
    birthDate: '1990-01-15',
    gender: 'Female',
    userTypeName: 'Member',
    userTypeColor: '#ff0000',
    assignments: [buildAttendeeAssignment()],
    ...overrides,
  }
}

/** Report summary of the default event. */
export function buildSummary(overrides: Partial<EventSummaryResponse> = {}): EventSummaryResponse {
  return {
    eventId: EVENT_ID,
    title: 'Hackathon',
    activitiesCount: 3,
    totalAssignments: 9,
    requestedAssignments: 1,
    confirmedAssignments: 7,
    deniedAssignments: 1,
    distinctVolunteers: 5,
    ratingsCount: 2,
    scoredRatingsCount: 2,
    ratingsAverage: 4.25,
    roleTypeBreakdown: [
      { roleTypeId: 'role-1', roleTypeName: 'Volunteer', approvedAssignments: 7 },
    ],
    ...overrides,
  }
}

/** Badge of the default attendee. */
export function buildBadge(overrides: Partial<EventBadgeResponse> = {}): EventBadgeResponse {
  return {
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    userTypeName: 'Member',
    userTypeColor: '#123456',
    createdAt: '2025-01-01T10:00:00Z',
    activities: [{ title: 'Robotics', location: 'Lab 1' }],
    ...overrides,
  }
}

/** Confirmed participant listed on the printable roster. */
export function buildRosterParticipant(
  overrides: Partial<EventRosterParticipantResponse> = {},
): EventRosterParticipantResponse {
  return {
    userId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    birthDate: '2000-01-01',
    email: 'ada@example.test',
    phone: '600000001',
    roleName: 'Monitora',
    ...overrides,
  }
}

/** Activity of the printable roster with the default participant. */
export function buildRosterActivity(
  overrides: Partial<EventRosterActivityResponse> = {},
): EventRosterActivityResponse {
  return {
    activityId: 'act-1',
    title: 'Robotics',
    location: 'Room 1',
    activityStartsAt: '2026-10-10T09:00:00Z',
    activityEndsAt: '2026-10-10T11:00:00Z',
    participants: [buildRosterParticipant()],
    ...overrides,
  }
}

/** Dashboard analytics with data in every chart. */
export function buildDashboardAnalytics(
  overrides: Partial<DashboardAnalyticsResponse> = {},
): DashboardAnalyticsResponse {
  return {
    rangeStart: '2025-09-17',
    rangeEnd: '2026-09-17',
    granularity: 'month',
    kpis: [
      { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
      { key: 'members', total: 80, inRange: 2, previousRange: 4 },
      { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
      { key: 'events', total: 12, inRange: 0, previousRange: 0 },
    ],
    userGrowth: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'member', values: [1, 2] }],
    },
    inscriptions: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'confirmed', values: [3, 4] }],
    },
    contentPublished: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'resources', values: [0, 1] }],
    },
    eventsCalendar: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'past', values: [1, 0] }],
    },
    usersByType: [{ key: 'member', count: 10 }],
    audienceComposition: [{ key: 'adults', count: 7 }],
    participantsByGender: [{ key: 'Female', count: 5 }],
    eventsByCategory: [{ key: 'cat-1', label: 'Programación', color: '#ff6600', count: 3 }],
    topEvents: [{ eventId: 'event-1', title: 'Hackathon de primavera', confirmed: 42 }],
    occupancy: {
      confirmed: 30,
      desired: 40,
      events: [
        {
          eventId: 'event-1',
          title: 'Hackathon de primavera',
          confirmed: 30,
          desired: 40,
          activities: [],
        },
      ],
    },
    ...overrides,
  }
}
