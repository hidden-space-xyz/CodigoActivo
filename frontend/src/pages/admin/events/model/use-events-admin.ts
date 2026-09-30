import { computed } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  eventList,
  eventMutations,
  eventQueries,
  type EventDetail,
  type EventInput,
  type EventListing,
} from '@/entities/event'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin events list: a table ordered by start date with a filter per column, the create/edit
 * dialog, which loads the whole event before editing it, featuring one event and deletion after
 * confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useEventsAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const table = useServerTable({
    ...eventList,
    defaultSort: { field: 'eventStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
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
  const create = useMutation(eventMutations.create())
  const update = useMutation(eventMutations.update())
  const remove = useMutation(eventMutations.remove())
  const featuring = useMutation(eventMutations.feature())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: EventInput): void {
    const event = dialog.editing.value
    if (event) {
      update.mutate(
        { id: event.id, input },
        feedback.outcome(t('pages.admin.events.toasts.updated'), dialog.close),
      )
      return
    }
    create.mutate(input, feedback.outcome(t('pages.admin.events.toasts.created'), dialog.close))
  }

  function feature(event: EventListing): void {
    if (event.featured) return
    featuring.mutate(event.id, feedback.outcome(t('pages.admin.events.toasts.featured')))
  }

  function confirmRemove(event: EventListing): void {
    confirmDelete({
      header: t('pages.admin.events.delete.header'),
      message: t('pages.admin.events.delete.message', { title: event.title }),
      accept: () => {
        remove.mutate(event.id, feedback.outcome(t('pages.admin.events.toasts.deleted')))
      },
    })
  }

  return {
    table,
    dialog,
    saving,
    featuring: featuring.isPending,
    save,
    feature,
    confirmRemove,
  }
}
