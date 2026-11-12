import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  inventoryEntryList,
  inventoryMutations,
  type InventoryCategory,
  type InventoryEntry,
} from '@/entities/inventory'
import { INVENTORY_MAX_QUANTITY, INVENTORY_MIN_QUANTITY } from '@/shared/config'
import { useCrudActions, useCrudDialog } from '@/shared/lib/crud'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { useServerTable } from '@/shared/lib/paging'

import { entryLabel } from '../lib/entry-values'
import { filterColumns, selectEntries } from './entry-table'

/**
 * Entries of a category: a table with a filter per column, sorted by the first one, the dialog
 * that adds or edits an entry, deletion after confirmation and the step that adds or takes one
 * from the quantity, each outcome reported with a toast. `category` is read on every use, so a
 * renamed column or option shows at once; a page re-creates it when the columns themselves change.
 * Call it in `setup`.
 */
export function useInventoryEntries(category: () => InventoryCategory) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const { id, columns } = category()
  const first = columns[0]

  const table = useServerTable({
    ...inventoryEntryList(id, (entries, params: Readonly<Record<string, unknown>>) =>
      selectEntries(category().columns, entries, params),
    ),
    defaultSort: first ? { field: first.id } : undefined,
    columns: filterColumns(columns),
  })
  const dialog = useCrudDialog<InventoryEntry>()
  const update = useMutation(inventoryMutations.updateEntry(id))
  const actions = useCrudActions({
    dialog,
    create: useMutation(inventoryMutations.addEntry(id)),
    update,
    remove: useMutation(inventoryMutations.removeEntry(id)),
    messages: {
      created: t('pages.admin.inventoryCategory.toasts.created'),
      updated: t('pages.admin.inventoryCategory.toasts.updated'),
      deleted: t('pages.admin.inventoryCategory.toasts.deleted'),
      deleteHeader: t('pages.admin.inventoryCategory.delete.header'),
      deleteMessage: (entry: InventoryEntry) =>
        t('pages.admin.inventoryCategory.delete.message', {
          name: entryLabel(category().columns, entry, t),
        }),
    },
  })

  /** Adds one to the quantity of an entry, or takes one from it, within the allowed range. */
  function step(entry: InventoryEntry, by: -1 | 1): void {
    const quantity = Math.min(
      INVENTORY_MAX_QUANTITY,
      Math.max(INVENTORY_MIN_QUANTITY, (entry.quantity ?? 0) + by),
    )
    update.mutate(
      { id: entry.id, input: { quantity, values: entry.values } },
      {
        onError: (error) => {
          feedback.error(error)
        },
      },
    )
  }

  return { table, dialog, step, stepping: update.isPending, ...actions }
}
