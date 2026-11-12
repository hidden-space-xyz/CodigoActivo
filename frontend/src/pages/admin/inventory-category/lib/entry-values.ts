import {
  rawValue,
  type InventoryColumn,
  type InventoryEntry,
  type InventoryOption,
} from '@/entities/inventory'
import { LOCALE } from '@/shared/config'
import type { Translate } from '@/shared/i18n'
import { formatDate } from '@/shared/lib/date'

const numberFormatter = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 20 })

/** Option of a list column that an entry chose, if any. */
export function chosenOption(
  column: InventoryColumn,
  entry: InventoryEntry,
): InventoryOption | undefined {
  const value = rawValue(column, entry)
  return column.options.find((option) => option.id === value)
}

/**
 * Value of a column in an entry as a person reads it: a number in the Spanish locale, a day as the
 * site writes dates, «Sí» or «No», or the name of the chosen option; empty text when it has none.
 */
export function displayValue(column: InventoryColumn, entry: InventoryEntry, t: Translate): string {
  const value = rawValue(column, entry)
  if (value === undefined) return ''
  switch (column.type) {
    case 'Number':
    case 'Quantity':
      return numberFormatter.format(value as Intl.StringNumericLiteral)
    case 'Date':
      return formatDate(value)
    case 'Boolean':
      return t(
        value === 'true' ? 'pages.admin.inventoryCategory.yes' : 'pages.admin.inventoryCategory.no',
      )
    case 'List':
      return chosenOption(column, entry)?.name ?? ''
    default:
      return value
  }
}

/** How an entry is named when asking before deleting it: its first value, or «esta fila». */
export function entryLabel(
  columns: readonly InventoryColumn[],
  entry: InventoryEntry,
  t: Translate,
): string {
  for (const column of columns) {
    const value = displayValue(column, entry, t)
    if (value) return value
  }
  return t('pages.admin.inventoryCategory.delete.unnamed')
}
