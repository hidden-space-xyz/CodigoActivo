import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  activityList,
  activityMutations,
  activityQueries,
  type ActivityDetail,
  type ActivityInput,
  type ActivityListing,
} from '@/entities/activity'
import { alsoInvalidates } from '@/shared/api'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

import { eventReportKeys } from '../api/queries'

/**
 * Activities tab of an event's admin page: a table ordered by start time with title, date and
 * modality filters, the create/edit dialog, which loads the whole activity before editing it, and
 * deletion after confirmation. Every change also refreshes the event's counts and attendees. Call
 * it in `setup`.
 *
 * @param eventId - Getter so the table follows route changes.
 */
export function useEventActivitiesAdmin(eventId: () => string) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const modalityId = ref<string | null>(null)
  const table = useServerTable({
    ...activityList,
    defaultSort: { field: 'activityStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      activityDate: { type: 'dateRange', fromParam: 'activityDateFrom', toParam: 'activityDateTo' },
    },
    extraParams: () => ({ eventId: eventId(), modalityTypeId: modalityId.value ?? undefined }),
  })
  const dialog = useCrudDialog<ActivityListing, ActivityDetail>({
    load: (row) => queryClient.fetchQuery({ ...activityQueries.detail(row.id), staleTime: 0 }),
    onMissing: () => {
      feedback.error(t('pages.admin.eventDetail.toast.activityNotFound'))
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  const reports = () => [eventReportKeys.stats(eventId()), eventReportKeys.attendees()] as const
  const create = useMutation(() =>
    alsoInvalidates(activityMutations.create(eventId()), ...reports()),
  )
  const update = useMutation(() => alsoInvalidates(activityMutations.update(), ...reports()))
  const remove = useMutation(() => alsoInvalidates(activityMutations.remove(), ...reports()))
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: ActivityInput): void {
    const activity = dialog.editing.value
    if (activity) {
      update.mutate(
        { id: activity.id, input },
        feedback.outcome(t('pages.admin.eventDetail.toast.activityUpdated'), dialog.close),
      )
      return
    }
    create.mutate(
      input,
      feedback.outcome(t('pages.admin.eventDetail.toast.activityCreated'), dialog.close),
    )
  }

  function confirmRemove(activity: ActivityListing): void {
    confirmDelete({
      header: t('pages.admin.eventDetail.deleteConfirm.header'),
      message: t('pages.admin.eventDetail.deleteConfirm.message', { title: activity.title }),
      accept: () => {
        remove.mutate(
          activity.id,
          feedback.outcome(t('pages.admin.eventDetail.toast.activityDeleted')),
        )
      },
    })
  }

  return { table, modalityId, dialog, saving, save, confirmRemove }
}
