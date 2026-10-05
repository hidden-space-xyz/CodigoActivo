import type {
  ActivityAssignment,
  EventActivity,
  HouseholdActivityAssignment,
} from '@/entities/activity'

import type { TimelineActivity, TimelineMemberAssignment } from '../model/types'

/** A moment of the timeline: activities whose schedules overlap, headed by the earliest start. */
export interface TimelineCluster {
  readonly start: Date
  readonly items: TimelineActivity[]
}

/**
 * Merges an event's activities with the signups of the user and their household. High-demand roles
 * are only flagged once `showDemand` says the user's own signups are known, and activities that
 * begin at or before `now` are marked as started.
 */
export function toTimelineActivities(
  activities: readonly EventActivity[],
  assignments: readonly ActivityAssignment[],
  householdAssignments: readonly HouseholdActivityAssignment[],
  showDemand: boolean,
  now: Date,
): TimelineActivity[] {
  const ownByActivity = new Map<string, { status: string; roleName: string }>()
  for (const assignment of assignments) {
    ownByActivity.set(assignment.activityId, {
      status: assignment.status,
      roleName: assignment.roleName,
    })
  }
  const householdByActivity = new Map<string, TimelineMemberAssignment[]>()
  for (const assignment of householdAssignments) {
    const members = householdByActivity.get(assignment.activityId) ?? []
    members.push({
      userId: assignment.userId,
      name: assignment.name,
      roleName: assignment.roleName,
      status: assignment.status,
    })
    householdByActivity.set(assignment.activityId, members)
  }

  return activities.map((activity) => ({
    id: activity.id,
    title: activity.title,
    description: activity.description,
    location: activity.location,
    modality: activity.modality,
    start: new Date(activity.startsAt),
    end: new Date(activity.endsAt),
    started: new Date(activity.startsAt).getTime() <= now.getTime(),
    highDemandRoleIds: showDemand ? [...activity.highDemandRoleIds] : [],
    assignment: ownByActivity.get(activity.id) ?? null,
    household: householdByActivity.get(activity.id) ?? [],
  }))
}

/**
 * Groups activities, in their given start order, into clusters whose schedules overlap: an
 * activity joins the current cluster while it starts before every earlier one in it has ended.
 */
export function toTimeline(activities: readonly TimelineActivity[]): TimelineCluster[] {
  const clusters: TimelineCluster[] = []
  let current: TimelineCluster | null = null
  let latestEnd = 0
  for (const activity of activities) {
    const start = activity.start.getTime()
    const end = activity.end.getTime()
    if (current && start < latestEnd) {
      current.items.push(activity)
      latestEnd = Math.max(latestEnd, end)
    } else {
      current = { start: activity.start, items: [activity] }
      clusters.push(current)
      latestEnd = end
    }
  }
  return clusters
}
