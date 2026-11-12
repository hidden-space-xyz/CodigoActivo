import { queryOptions } from '@tanstack/vue-query'

import type { ServerTablePage, ServerTableSource } from '@/shared/lib/paging'

import type {
  InventoryCategoryListParams,
  InventoryCategorySummary,
  InventoryEntry,
} from '../model/types'
import {
  getInventoryCategoryPageRequest,
  getInventoryCategoryRequest,
  getInventoryEntriesRequest,
} from './requests'

/** Query keys of the inventory; `all` covers the categories and their entries. */
export const inventoryKeys = {
  all: ['inventory'] as const,
  list: () => [...inventoryKeys.all, 'list'] as const,
  category: (id: string) => [...inventoryKeys.all, 'category', id] as const,
  entries: (categoryId: string) => [...inventoryKeys.all, 'entries', categoryId] as const,
}

/** Query options of the inventory. */
export const inventoryQueries = {
  /** A category with its columns, `null` when it does not exist. */
  category: (id: string) =>
    queryOptions({
      queryKey: inventoryKeys.category(id),
      queryFn: () => getInventoryCategoryRequest(id),
    }),
  /** Every entry of a category. */
  entries: (categoryId: string) =>
    queryOptions({
      queryKey: inventoryKeys.entries(categoryId),
      queryFn: () => getInventoryEntriesRequest(categoryId),
    }),
}

/** List of categories with their totals, paged, filtered and sorted by the API. */
export const inventoryCategoryList: ServerTableSource<
  InventoryCategorySummary,
  InventoryCategoryListParams
> = {
  queryKey: inventoryKeys.list(),
  fetchPage: (params) => getInventoryCategoryPageRequest(params),
}

/**
 * Entries of a category for a table: the API returns all of them, at most
 * `INVENTORY_MAX_ENTRIES`, and `select` filters, sorts and pages them by the table's params.
 */
export function inventoryEntryList<TParams>(
  categoryId: string,
  select: (entries: readonly InventoryEntry[], params: TParams) => ServerTablePage<InventoryEntry>,
): ServerTableSource<InventoryEntry, TParams> {
  return {
    queryKey: inventoryKeys.entries(categoryId),
    fetchPage: async (params) => select(await getInventoryEntriesRequest(categoryId), params),
  }
}
