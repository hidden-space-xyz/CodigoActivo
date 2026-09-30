import { computed } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  eventCategoryList,
  eventCategoryMutations,
  type EventCategory,
  type EventCategoryInput,
} from '@/entities/event-category'
import { useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Event category catalog: a table sorted by name and filterable by name and color, the create/edit
 * dialog and deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useEventCategoriesAdmin() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const table = useServerTable({
    ...eventCategoryList,
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
      color: { type: 'text' },
    },
  })
  const dialog = useCrudDialog<EventCategory>()
  const create = useMutation(eventCategoryMutations.create())
  const update = useMutation(eventCategoryMutations.update())
  const remove = useMutation(eventCategoryMutations.remove())
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: EventCategoryInput): void {
    const category = dialog.editing.value
    if (category) {
      update.mutate(
        { id: category.id, input },
        feedback.outcome(t('pages.admin.catalogs.eventCategories.toasts.updated'), dialog.close),
      )
      return
    }
    create.mutate(
      input,
      feedback.outcome(t('pages.admin.catalogs.eventCategories.toasts.created'), dialog.close),
    )
  }

  function confirmRemove(category: EventCategory): void {
    confirmDelete({
      header: t('pages.admin.catalogs.eventCategories.delete.header'),
      message: t('pages.admin.catalogs.eventCategories.delete.message', { name: category.name }),
      accept: () => {
        remove.mutate(
          category.id,
          feedback.outcome(t('pages.admin.catalogs.eventCategories.toasts.deleted')),
        )
      },
    })
  }

  return { table, dialog, saving, save, confirmRemove }
}
