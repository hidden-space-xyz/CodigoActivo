import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  resourceList,
  resourceMutations,
  resourceQueries,
  type LearningResource,
  type LearningResourceSummary,
  type ResourceInput,
} from '@/entities/resource'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Admin resource list: a table of the newest resources with a filter per column, the create/edit
 * dialog, which loads the whole resource before editing it, and deletion after confirmation, each
 * outcome reported with a toast. Call it in `setup`.
 */
export function useResourcesAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const table = useServerTable({
    ...resourceList,
    defaultSort: { field: 'createdAt', order: -1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
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
  const create = useMutation(resourceMutations.create())
  const update = useMutation(resourceMutations.update())
  const remove = useMutation(resourceMutations.remove())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: ResourceInput): void {
    const resource = dialog.editing.value
    if (resource) {
      update.mutate(
        { id: resource.id, input },
        feedback.outcome(t('pages.admin.resources.toasts.updated'), dialog.close),
      )
      return
    }
    create.mutate(input, feedback.outcome(t('pages.admin.resources.toasts.created'), dialog.close))
  }

  function confirmRemove(resource: LearningResourceSummary): void {
    confirmDelete({
      header: t('pages.admin.resources.delete.header'),
      message: t('pages.admin.resources.delete.message', { title: resource.title }),
      accept: () => {
        remove.mutate(resource.id, feedback.outcome(t('pages.admin.resources.toasts.deleted')))
      },
    })
  }

  return { table, types, dialog, saving, save, confirmRemove }
}
