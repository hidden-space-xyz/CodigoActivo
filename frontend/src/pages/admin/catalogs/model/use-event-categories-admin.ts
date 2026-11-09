import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  eventCategoryList,
  eventCategoryMutations,
  type EventCategory,
} from '@/entities/event-category'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useServerTable } from '@/shared/lib/paging'

/**
 * Event category catalog: a table sorted and filterable by name, the create/edit dialog and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useEventCategoriesAdmin() {
  const { t } = useI18n()

  const table = useServerTable({
    ...eventCategoryList,
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
    },
  })
  const dialog = useCrudDialog<EventCategory>()
  const actions = useCrudActions({
    dialog,
    create: useMutation(eventCategoryMutations.create()),
    update: useMutation(eventCategoryMutations.update()),
    remove: useMutation(eventCategoryMutations.remove()),
    messages: {
      created: t('pages.admin.catalogs.eventCategories.toasts.created'),
      updated: t('pages.admin.catalogs.eventCategories.toasts.updated'),
      deleted: t('pages.admin.catalogs.eventCategories.toasts.deleted'),
      deleteHeader: t('pages.admin.catalogs.eventCategories.delete.header'),
      deleteMessage: (category: EventCategory) =>
        t('pages.admin.catalogs.eventCategories.delete.message', { name: category.name }),
    },
  })

  return { table, dialog, ...actions }
}
