import type {
  AddInventoryEntryRequest,
  CreateInventoryCategoryRequest,
  InventoryCategoryResponse,
  InventoryCategorySummaryResponse,
  InventoryColumnResponse,
  InventoryEntryResponse,
} from '@/shared/api/generated/models'

import type {
  InventoryCategory,
  InventoryCategoryInput,
  InventoryCategorySummary,
  InventoryColumn,
  InventoryEntry,
  InventoryEntryInput,
} from '../model/types'

/** Maps a category of the list of categories. */
export function toInventoryCategorySummary(
  category: InventoryCategorySummaryResponse,
): InventoryCategorySummary {
  return {
    id: category.id,
    name: category.name,
    entryCount: category.entryCount,
    total: category.total,
  }
}

function toInventoryColumn(column: InventoryColumnResponse): InventoryColumn {
  return {
    id: column.id,
    name: column.name,
    type: column.type,
    required: column.required,
    options: column.options.map((option) => ({
      id: option.id,
      name: option.name,
      color: option.color,
    })),
  }
}

/** Maps a category with its columns. */
export function toInventoryCategory(category: InventoryCategoryResponse): InventoryCategory {
  return {
    ...toInventoryCategorySummary(category),
    columns: category.columns.map(toInventoryColumn),
  }
}

/** Maps an entry; a missing quantity becomes `null`. */
export function toInventoryEntry(entry: InventoryEntryResponse): InventoryEntry {
  return { id: entry.id, quantity: entry.quantity ?? null, values: { ...entry.values } }
}

/** Builds the body that creates or redefines a category; both endpoints take the same fields. */
export function toInventoryCategoryRequest(
  input: InventoryCategoryInput,
): CreateInventoryCategoryRequest {
  return {
    name: input.name,
    columns: input.columns.map((column) => ({
      id: column.id,
      name: column.name,
      type: column.type,
      required: column.required,
      options: column.options.map((option) => ({
        id: option.id,
        name: option.name,
        color: option.color,
      })),
    })),
  }
}

/** Builds the body that adds or replaces an entry; both endpoints take the same fields. */
export function toInventoryEntryRequest(input: InventoryEntryInput): AddInventoryEntryRequest {
  return { quantity: input.quantity, values: { ...input.values } }
}
