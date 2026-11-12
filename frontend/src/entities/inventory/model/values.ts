import type { InventoryColumn, InventoryEntry } from './types'

/** Value of a column in an entry as stored, the quantity as text; `undefined` when it has none. */
export function rawValue(column: InventoryColumn, entry: InventoryEntry): string | undefined {
  if (column.type === 'Quantity')
    return entry.quantity === null ? undefined : String(entry.quantity)
  return entry.values[column.id]
}
