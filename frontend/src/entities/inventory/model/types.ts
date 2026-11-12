/** Kind of value a column of an inventory category holds. */
export type InventoryColumnType =
  'Text' | 'LongText' | 'Number' | 'Date' | 'Boolean' | 'List' | 'Quantity'

/** One of the choices of a list column; `color` is a `#rrggbb` hex color. */
export interface InventoryOption {
  readonly id: string
  readonly name: string
  readonly color: string
}

/** Column an administrator defined for a category; `options` is empty unless it is a list. */
export interface InventoryColumn {
  readonly id: string
  readonly name: string
  readonly type: InventoryColumnType
  readonly required: boolean
  readonly options: readonly InventoryOption[]
}

/**
 * Inventory category as the list of categories shows it; `total` adds up its quantity column, or
 * counts its entries when it has none.
 */
export interface InventoryCategorySummary {
  readonly id: string
  readonly name: string
  readonly entryCount: number
  readonly total: number
}

/** Inventory category with its columns in order. */
export interface InventoryCategory extends InventoryCategorySummary {
  readonly columns: readonly InventoryColumn[]
}

/**
 * Row of an inventory category: the value of its quantity column, if any, and the value of each
 * other column that has one, keyed by column id. Values are text as written, a number with a dot,
 * a `YYYY-MM-DD` day, `true` or `false`, or the id of the chosen option.
 */
export interface InventoryEntry {
  readonly id: string
  readonly quantity: number | null
  readonly values: Readonly<Record<string, string>>
}

/** What an option of a list column should become; `id` is `null` for a new one. */
interface InventoryOptionInput {
  readonly id: string | null
  readonly name: string
  readonly color: string
}

/** What a column should become; `id` is `null` for a new one. */
interface InventoryColumnInput {
  readonly id: string | null
  readonly name: string
  readonly type: InventoryColumnType
  readonly required: boolean
  readonly options: readonly InventoryOptionInput[]
}

/** Values a category is created or redefined with; a column left out is removed. */
export interface InventoryCategoryInput {
  readonly name: string
  readonly columns: readonly InventoryColumnInput[]
}

/** Values an entry is added or replaced with, written as `InventoryEntry` holds them. */
export interface InventoryEntryInput {
  readonly quantity: number | null
  readonly values: Readonly<Record<string, string>>
}

/** Filters, sort and page of the list of categories. */
export interface InventoryCategoryListParams {
  readonly name?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
