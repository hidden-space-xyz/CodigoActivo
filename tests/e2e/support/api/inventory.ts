import type { AddInventoryEntryRequest } from '@contract/addInventoryEntryRequest'
import type { CreateInventoryCategoryRequest } from '@contract/createInventoryCategoryRequest'
import type { InventoryCategoryResponse } from '@contract/inventoryCategoryResponse'
import type { InventoryCategorySummaryResponse } from '@contract/inventoryCategorySummaryResponse'
import type { InventoryCategorySummaryResponsePagedResult } from '@contract/inventoryCategorySummaryResponsePagedResult'
import type { InventoryColumnOptionResponse } from '@contract/inventoryColumnOptionResponse'
import type { InventoryColumnResponse } from '@contract/inventoryColumnResponse'
import type { InventoryColumnType } from '@contract/inventoryColumnType'
import type { InventoryEntryResponse } from '@contract/inventoryEntryResponse'

import { uniqueTitle } from '../data/unique'
import type { ApiClient } from './api-client'

const OPTION_COLOR = '#16A34A'

/**
 * A column of an inventory category: its name, its type, whether every row must fill it and, for a
 * list, the names of its options in order.
 */
export interface InventoryColumnDefinition {
  readonly name: string
  readonly type: InventoryColumnType
  readonly required?: boolean
  readonly options?: readonly string[]
}

/** Creates an inventory category with `columns`, in that order, and a unique name. */
export function createInventoryCategory(
  admin: ApiClient,
  columns: readonly InventoryColumnDefinition[],
): Promise<InventoryCategoryResponse> {
  const request: CreateInventoryCategoryRequest = {
    name: uniqueTitle('Inventario'),
    columns: columns.map((column) => ({
      id: null,
      name: column.name,
      type: column.type,
      required: column.required ?? false,
      options: (column.options ?? []).map((option) => ({
        id: null,
        name: option,
        color: OPTION_COLOR,
      })),
    })),
  }
  return admin.post('/api/inventory/categories', request)
}

/**
 * Adds a row to `category` with `values`, keyed by column name, and `quantity` in its quantity
 * column, if any.
 */
export function addInventoryEntry(
  admin: ApiClient,
  category: InventoryCategoryResponse,
  values: Readonly<Record<string, string>>,
  quantity: number | null = null,
): Promise<InventoryEntryResponse> {
  const request: AddInventoryEntryRequest = {
    quantity,
    values: inventoryValues(category, values),
  }
  return admin.post(`/api/inventory/categories/${category.id}/entries`, request)
}

/** Everything the API stores about the inventory category `categoryId`, its columns included. */
export function inventoryCategoryById(
  admin: ApiClient,
  categoryId: string,
): Promise<InventoryCategoryResponse> {
  return admin.get<InventoryCategoryResponse>(`/api/inventory/categories/${categoryId}`)
}

/** The listed inventory categories named exactly `name`. */
export async function inventoryCategoriesNamed(
  admin: ApiClient,
  name: string,
): Promise<InventoryCategorySummaryResponse[]> {
  const { items } = await admin.get<InventoryCategorySummaryResponsePagedResult>(
    '/api/inventory/categories',
    { name, pageSize: 25 },
  )
  return items.filter((item) => item.name === name)
}

/** Everything the API stores about the inventory category named exactly `name`. */
export async function inventoryCategoryNamed(
  admin: ApiClient,
  name: string,
): Promise<InventoryCategoryResponse> {
  const [listed] = await inventoryCategoriesNamed(admin, name)
  if (!listed) throw new Error(`No inventory category is named «${name}»`)
  return inventoryCategoryById(admin, listed.id)
}

/** Every row of the inventory category `categoryId`. */
export function inventoryEntries(
  admin: ApiClient,
  categoryId: string,
): Promise<InventoryEntryResponse[]> {
  return admin.get<InventoryEntryResponse[]>(`/api/inventory/categories/${categoryId}/entries`)
}

/** The column of `category` named `name`, failing when it has none. */
export function inventoryColumn(
  category: InventoryCategoryResponse,
  name: string,
): InventoryColumnResponse {
  const column = category.columns.find((candidate) => candidate.name === name)
  if (!column) throw new Error(`The category «${category.name}» has no column «${name}»`)
  return column
}

/** The option of the list `column` named `name`, failing when it has none. */
export function inventoryOption(
  column: InventoryColumnResponse,
  name: string,
): InventoryColumnOptionResponse {
  const option = column.options.find((candidate) => candidate.name === name)
  if (!option) throw new Error(`The column «${column.name}» has no option «${name}»`)
  return option
}

/**
 * The values of a row as the API takes and returns them, keyed by column id, from `values` keyed by
 * column name: a list column takes the name of its option and the API keeps its id.
 */
export function inventoryValues(
  category: InventoryCategoryResponse,
  values: Readonly<Record<string, string>>,
): Record<string, string> {
  return Object.fromEntries(
    Object.entries(values).map(([name, value]) => {
      const column = inventoryColumn(category, name)
      return [column.id, column.type === 'List' ? inventoryOption(column, value).id : value]
    }),
  )
}
