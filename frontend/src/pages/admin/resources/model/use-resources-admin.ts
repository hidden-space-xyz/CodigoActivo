import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  resourceList,
  resourceMutations,
  resourceQueries,
  type LearningResource,
  type LearningResourceSummary,
} from '@/entities/resource'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin resource list: a table of the newest resources searched by title or subtitle and filtered
 * by type, link and creation date, the create/edit dialog, which loads the whole resource before
 * editing it, and deletion after confirmation, each outcome reported with a toast. Call it in
 * `setup`.
 */
export function useResourcesAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()

  const table = useServerTable({
    ...resourceList,
    defaultSort: { field: 'createdAt', order: -1 },
    columns: {
      search: { type: 'text' },
      type: { param: 'resourceTypeId' },
      url: { type: 'text' },
      created: { type: 'dateRange', fromParam: 'createdFrom', toParam: 'createdTo' },
    },
  })
  const types = useQuery(resourceQueries.types())
  const dialog = useCrudDialog<LearningResourceSummary, LearningResource>({
    load: (row) => queryClient.fetchQuery({ ...resourceQueries.detail(row.id), staleTime: 0 }),
    onMissing: () => {
      feedback.error(t('pages.admin.resources.toasts.notFound'))
    },
    onError: (error) => {
      feedback.error(error)
    },
  })
  const actions = useCrudActions({
    dialog,
    create: useMutation(resourceMutations.create()),
    update: useMutation(resourceMutations.update()),
    remove: useMutation(resourceMutations.remove()),
    messages: {
      created: t('pages.admin.resources.toasts.created'),
      updated: t('pages.admin.resources.toasts.updated'),
      deleted: t('pages.admin.resources.toasts.deleted'),
      deleteHeader: t('pages.admin.resources.delete.header'),
      deleteMessage: (resource: LearningResourceSummary) =>
        t('pages.admin.resources.delete.message', { title: resource.title }),
    },
  })

  return { table, types, dialog, ...actions }
}
