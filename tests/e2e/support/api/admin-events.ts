import type { EventListItemResponse } from '@contract/eventListItemResponse'
import type { EventListItemResponsePagedResult } from '@contract/eventListItemResponsePagedResult'
import type { EventResponse } from '@contract/eventResponse'

import type { ApiClient } from './api-client'

/** The event titled exactly `title` among past and upcoming ones, or `undefined` when none is. */
export async function findEvent(
  admin: ApiClient,
  title: string,
): Promise<EventListItemResponse | undefined> {
  const { items } = await admin.get<EventListItemResponsePagedResult>('/api/events', {
    search: title,
    pageSize: 25,
  })
  return items.find((item) => item.title === title)
}

/** Everything the API stores about the event `eventId`, its terms documents included. */
export function eventById(admin: ApiClient, eventId: string): Promise<EventResponse> {
  return admin.get<EventResponse>(`/api/events/${eventId}`)
}

/** Everything the API stores about the event titled exactly `title`, failing when none is. */
export async function eventTitled(admin: ApiClient, title: string): Promise<EventResponse> {
  const listed = await findEvent(admin, title)
  if (!listed) throw new Error(`No event is titled «${title}»`)
  return eventById(admin, listed.id)
}
