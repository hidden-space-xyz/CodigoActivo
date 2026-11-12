import {
  deleteApiInventoryCategoriesCategoryId,
  deleteApiInventoryCategoriesCategoryIdEntriesEntryId,
  getApiInventoryCategories,
  getApiInventoryCategoriesCategoryId,
  getApiInventoryCategoriesCategoryIdEntries,
  postApiInventoryCategories,
  postApiInventoryCategoriesCategoryIdEntries,
  putApiInventoryCategoriesCategoryId,
  putApiInventoryCategoriesCategoryIdEntriesEntryId,
} from '@/shared/api/generated/endpoints/inventory/inventory'
import { toPage, unwrapOrNull } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type {
  InventoryCategory,
  InventoryCategoryInput,
  InventoryCategoryListParams,
  InventoryCategorySummary,
  InventoryEntry,
  InventoryEntryInput,
} from '../model/types'
import {
  toInventoryCategory,
  toInventoryCategoryRequest,
  toInventoryCategorySummary,
  toInventoryEntry,
  toInventoryEntryRequest,
} from './mapper'

/** Fetches one page of the list of categories. */
export async function getInventoryCategoryPageRequest(
  params: InventoryCategoryListParams,
): Promise<ServerTablePage<InventoryCategorySummary>> {
  return toPage(await getApiInventoryCategories(params), toInventoryCategorySummary)
}

/** Loads a category with its columns, or `null` when it does not exist. */
export async function getInventoryCategoryRequest(id: string): Promise<InventoryCategory | null> {
  const category = await unwrapOrNull(getApiInventoryCategoriesCategoryId(id))
  return category ? toInventoryCategory(category) : null
}

/** Loads every entry of a category. */
export async function getInventoryEntriesRequest(
  categoryId: string,
): Promise<readonly InventoryEntry[]> {
  const response = await getApiInventoryCategoriesCategoryIdEntries(categoryId)
  return response.data.map(toInventoryEntry)
}

/** Creates a category with its columns. */
export async function createInventoryCategoryRequest(input: InventoryCategoryInput): Promise<void> {
  await postApiInventoryCategories(toInventoryCategoryRequest(input))
}

/** Renames a category and replaces its columns. */
export async function updateInventoryCategoryRequest(
  id: string,
  input: InventoryCategoryInput,
): Promise<void> {
  await putApiInventoryCategoriesCategoryId(id, toInventoryCategoryRequest(input))
}

/** Deletes a category with its entries. */
export async function deleteInventoryCategoryRequest(id: string): Promise<void> {
  await deleteApiInventoryCategoriesCategoryId(id)
}

/** Adds an entry to a category. */
export async function addInventoryEntryRequest(
  categoryId: string,
  input: InventoryEntryInput,
): Promise<void> {
  await postApiInventoryCategoriesCategoryIdEntries(categoryId, toInventoryEntryRequest(input))
}

/** Replaces the values of an entry. */
export async function updateInventoryEntryRequest(
  categoryId: string,
  id: string,
  input: InventoryEntryInput,
): Promise<void> {
  await putApiInventoryCategoriesCategoryIdEntriesEntryId(
    categoryId,
    id,
    toInventoryEntryRequest(input),
  )
}

/** Deletes an entry. */
export async function deleteInventoryEntryRequest(categoryId: string, id: string): Promise<void> {
  await deleteApiInventoryCategoriesCategoryIdEntriesEntryId(categoryId, id)
}
