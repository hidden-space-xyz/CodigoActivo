import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  inventoryCategoryList,
  inventoryMutations,
  inventoryQueries,
  type InventoryCategorySummary,
} from '@/entities/inventory'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

import { columnUsage, type EditedInventoryCategory } from './category-form'

/**
 * List of inventory categories: a table sorted by name with their totals, the dialog that creates
 * a category or redefines its columns, loading first what its entries say about each column, and
 * deletion after confirmation, each outcome reported with a toast. Call it in `setup`.
 */
export function useInventoryAdmin() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const feedback = useCrudFeedback()

  const table = useServerTable({
    ...inventoryCategoryList,
    defaultSort: { field: 'name', order: 1 },
    columns: { name: { type: 'text' } },
  })

  async function load(row: InventoryCategorySummary): Promise<EditedInventoryCategory | null> {
    const [category, entries] = await Promise.all([
      queryClient.fetchQuery({ ...inventoryQueries.category(row.id), staleTime: 0 }),
      queryClient.fetchQuery({ ...inventoryQueries.entries(row.id), staleTime: 0 }),
    ])
    if (!category) return null
    return {
      ...category,
      usage: Object.fromEntries(
        category.columns.map((column) => [column.id, columnUsage(column, entries)]),
      ),
    }
  }

  const dialog = useCrudDialog<InventoryCategorySummary, EditedInventoryCategory>({
    load,
    onMissing: () => {
      feedback.error(t('errors.InventoryCategoryNotFound'))
    },
    onError: (error) => {
      feedback.error(error)
    },
  })
  const actions = useCrudActions({
    dialog,
    create: useMutation(inventoryMutations.createCategory()),
    update: useMutation(inventoryMutations.updateCategory()),
    remove: useMutation(inventoryMutations.removeCategory()),
    messages: {
      created: t('pages.admin.inventory.toasts.created'),
      updated: t('pages.admin.inventory.toasts.updated'),
      deleted: t('pages.admin.inventory.toasts.deleted'),
      deleteHeader: t('pages.admin.inventory.delete.header'),
      deleteMessage: (category: InventoryCategorySummary) =>
        t('pages.admin.inventory.delete.message', { name: category.name }),
    },
  })

  return { table, dialog, ...actions }
}
