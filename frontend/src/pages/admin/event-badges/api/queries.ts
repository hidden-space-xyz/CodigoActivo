import { queryOptions } from '@tanstack/vue-query'

import { getEventBadgesRequest } from './requests'

/** Query keys of the badges report, under the root of every event report. */
export const badgeKeys = {
  all: ['reports'] as const,
  ofEvent: (eventId: string) => [...badgeKeys.all, 'event-badges', eventId] as const,
}

/** Query options of the badges report. */
export const badgeQueries = {
  /** Badges of every attendee of an event. */
  ofEvent: (eventId: string) =>
    queryOptions({
      queryKey: badgeKeys.ofEvent(eventId),
      queryFn: () => getEventBadgesRequest(eventId),
    }),
}
