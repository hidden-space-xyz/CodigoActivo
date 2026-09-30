import { useServerTable } from '@/shared/lib/paging'

import { ratingList } from '../api/queries'

/**
 * Opinions tab of an event's admin page: its anonymous ratings, best scores first. The list only
 * loads while the tab is `active`. Call it in `setup`.
 *
 * @param eventId - Getter so the list follows route changes.
 * @param active - Getter telling whether the tab is selected.
 */
export function useEventRatings(eventId: () => string, active: () => boolean) {
  return useServerTable({
    ...ratingList,
    defaultSort: { field: 'score', order: -1 },
    extraParams: () => ({ eventId: eventId() }),
    enabled: active,
  })
}
