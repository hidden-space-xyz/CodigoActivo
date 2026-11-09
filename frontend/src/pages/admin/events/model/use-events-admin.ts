import { readonly, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  eventList,
  eventMutations,
  eventQueries,
  type EventDetail,
  type EventListing,
} from '@/entities/event'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

type EventListScope = 'upcoming' | 'past' | 'all'

/**
 * Admin events list: a table of the upcoming events ordered by start date, with a scope switch to
 * the past events or all of them (newest first) and a filter per column, the create/edit dialog,
 * which loads the whole event before editing it, featuring one event and deletion after
 * confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useEventsAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const scope = ref<EventListScope>('upcoming')

  const table = useServerTable({
    ...eventList,
    defaultSort: { field: 'eventStartsAt', order: 1 },
    extraParams: () =>
      scope.value === 'all' ? {} : { scope: scope.value === 'upcoming' ? 'Upcoming' : 'Past' },
    columns: {
      search: { type: 'text' },
      category: { param: 'categoryTypeId' },
      eventDate: { type: 'dateRange', fromParam: 'eventDateFrom', toParam: 'eventDateTo' },
      signup: { type: 'dateRange', fromParam: 'signupFrom', toParam: 'signupTo' },
    },
  })
  const dialog = useCrudDialog<EventListing, EventDetail>({
    load: (row) => queryClient.fetchQuery({ ...eventQueries.detail(row.id), staleTime: 0 }),
    onMissing: () => {
      feedback.error(t('pages.admin.events.toasts.notFound'))
    },
    onError: (error) => {
      feedback.error(error)
    },
  })
  const actions = useCrudActions({
    dialog,
    create: useMutation(eventMutations.create()),
    update: useMutation(eventMutations.update()),
    remove: useMutation(eventMutations.remove()),
    messages: {
      created: t('pages.admin.events.toasts.created'),
      updated: t('pages.admin.events.toasts.updated'),
      deleted: t('pages.admin.events.toasts.deleted'),
      deleteHeader: t('pages.admin.events.delete.header'),
      deleteMessage: (event: EventListing) =>
        t('pages.admin.events.delete.message', { title: event.title }),
    },
  })
  const featuring = useMutation(eventMutations.feature())

  function setScope(next: EventListScope): void {
    scope.value = next
    table.setSort('eventStartsAt', next === 'upcoming' ? 1 : -1)
  }

  function feature(event: EventListing): void {
    if (event.featured) return
    featuring.mutate(event.id, feedback.outcome(t('pages.admin.events.toasts.featured')))
  }

  return {
    table,
    scope: readonly(scope),
    setScope,
    dialog,
    ...actions,
    featuring: featuring.isPending,
    feature,
  }
}
