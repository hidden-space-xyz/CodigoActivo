import { getApiReportsEventsEventIdBadges } from '@/shared/api/generated/endpoints/reports/reports'

import type { EventBadges } from '../model/types'
import { toEventBadges } from './mapper'

/** Loads one badge per attendee of an event. */
export async function getEventBadgesRequest(eventId: string): Promise<EventBadges> {
  const { data } = await getApiReportsEventsEventIdBadges(eventId)
  return toEventBadges(data)
}
