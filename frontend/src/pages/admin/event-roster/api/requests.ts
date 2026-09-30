import { getApiReportsEventsEventIdRoster } from '@/shared/api/generated/endpoints/reports/reports'

import type { EventRoster } from '../model/types'
import { toEventRoster } from './mapper'

/** Loads the participants of every activity of an event, with contact and guardian data. */
export async function getEventRosterRequest(eventId: string): Promise<EventRoster> {
  const { data } = await getApiReportsEventsEventIdRoster(eventId)
  return toEventRoster(data)
}
