import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  activityList,
  activityMutations,
  activityQueries,
  type ActivityDetail,
  type ActivityListing,
} from '@/entities/activity'
import { alsoInvalidates } from '@/shared/api'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
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

  const table = useServerTable({
    ...activityList,
    defaultSort: { field: 'activityStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      activityDate: { type: 'dateRange', fromParam: 'activityDateFrom', toParam: 'activityDateTo' },
      modality: { param: 'modalityTypeId' },
    },
    extraParams: () => ({ eventId: eventId() }),
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
  const actions = useCrudActions({
    dialog,
    create: useMutation(() => alsoInvalidates(activityMutations.create(eventId()), ...reports())),
    update: useMutation(() => alsoInvalidates(activityMutations.update(), ...reports())),
    remove: useMutation(() => alsoInvalidates(activityMutations.remove(), ...reports())),
    messages: {
      created: t('pages.admin.eventDetail.toast.activityCreated'),
      updated: t('pages.admin.eventDetail.toast.activityUpdated'),
      deleted: t('pages.admin.eventDetail.toast.activityDeleted'),
      deleteHeader: t('pages.admin.eventDetail.deleteConfirm.header'),
      deleteMessage: (activity: ActivityListing) =>
        t('pages.admin.eventDetail.deleteConfirm.message', { title: activity.title }),
    },
  })

  return { table, dialog, ...actions }
}
