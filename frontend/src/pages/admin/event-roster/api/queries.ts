import { queryOptions } from '@tanstack/vue-query'

import { getEventRosterRequest } from './requests'

/** Query keys of the roster report, under the root of every event report. */
export const rosterKeys = {
  all: ['reports'] as const,
  ofEvent: (eventId: string) => [...rosterKeys.all, 'event-roster', eventId] as const,
}

/** Query options of the roster report. */
export const rosterQueries = {
  /** Participants of every activity of an event. */
  ofEvent: (eventId: string) =>
    queryOptions({
      queryKey: rosterKeys.ofEvent(eventId),
      queryFn: () => getEventRosterRequest(eventId),
    }),
}
