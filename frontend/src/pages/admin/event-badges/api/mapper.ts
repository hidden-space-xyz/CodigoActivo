import type { EventBadgeResponse, EventBadgesResponse } from '@/shared/api/generated/models'

import type { Badge, EventBadges } from '../model/types'

function toBadge(badge: EventBadgeResponse): Badge {
  return {
    userId: badge.userId,
    firstName: badge.firstName,
    lastName: badge.lastName,
    userTypeName: badge.userTypeName,
    userTypeColor: badge.userTypeColor,
    guardian: badge.guardian
      ? { firstName: badge.guardian.firstName, phone: badge.guardian.phone ?? '' }
      : null,
    activities: badge.activities.map((activity) => ({
      title: activity.title,
      location: activity.location,
    })),
  }
}

/** Maps the badges report of an event. */
export function toEventBadges(report: EventBadgesResponse): EventBadges {
  return { title: report.title, badges: report.badges.map(toBadge) }
}
