import type { Gender } from '@/entities/user'

/** Signup count of one role, as a summary card shows it; `name` is empty when unknown. */
interface RoleStats {
  readonly name: string
  readonly approved: number
}

/** Activities, approved signups per role and ratings of an event, for its summary cards. */
export interface EventStats {
  readonly activitiesCount: number
  readonly roles: readonly RoleStats[]
  readonly ratingsCount: number
  /** Mean score out of 5; `null` until someone rates the event. */
  readonly ratingsAverage: number | null
}

/** Guardian of a minor attendee; contact fields are empty when unknown. */
interface AttendeeGuardian {
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly secondaryPhone: string
}

/** One signup of an attendee; names are empty when unknown. */
export interface AttendeeAssignment {
  readonly activityId: string
  readonly activityTitle: string
  readonly roleTypeId: string
  readonly roleName: string
  readonly statusId: string
  readonly statusName: string
  readonly signedUpAt: string
  /** The attendee has another signup of the event at the same time. */
  readonly hasTimeConflict: boolean
}

/**
 * Someone signed up to the event with their signups; empty strings stand for unknown data and
 * `guardian` is only set for minors.
 */
export interface EventAttendee {
  readonly userId: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly secondaryPhone: string
  readonly birthDate: string | null
  readonly gender: Gender
  readonly userTypeName: string
  readonly userTypeColor: string
  readonly guardian: AttendeeGuardian | null
  readonly assignments: readonly AttendeeAssignment[]
}

/** Filters of the attendees list; an unset filter keeps every attendee. */
export type AttendeeFilters = {
  readonly search?: string
  readonly userTypeId?: string
  readonly gender?: Gender
  readonly activityId?: string
  readonly roleTypeId?: string
  readonly statusId?: string
}

/** Event, filters, sort and page of the attendees list. */
export interface AttendeeListParams extends AttendeeFilters {
  readonly eventId: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}

/** Anonymous rating of an event; unanswered questions are empty. */
export interface EventRating {
  readonly id: string
  readonly score: number
  readonly mostLiked: string
  readonly leastLiked: string
  readonly suggestions: string
}

/** Event, sort and page of the ratings list. */
export interface RatingListParams {
  readonly eventId: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
