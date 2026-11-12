import type { InventoryColumn, InventoryEntry, InventoryEntryInput } from '@/entities/inventory'
import {
  INVENTORY_MAX_QUANTITY,
  INVENTORY_MIN_QUANTITY,
  INVENTORY_NUMBER_MAX_DECIMALS,
  INVENTORY_NUMBER_MAX_LENGTH,
  INVENTORY_NUMBER_MAX_VALUE,
} from '@/shared/config'
import type { FormProblem, FormReading } from '@/shared/lib/form'

const NUMBER = /^-?\d*\.?\d+$/

/**
 * What the entry dialog binds its inputs to: the text of each column by column id (a number as
 * typed, a `YYYY-MM-DD` day, `true` or `false`, or an option id) and the quantity apart.
 */
export interface InventoryEntryDraft {
  values: Record<string, string | null | undefined>
  quantity: number | null | undefined
}

/** A blank draft for the columns, or one filled with the entry being edited. */
export function toInventoryEntryDraft(
  columns: readonly InventoryColumn[],
  entry: InventoryEntry | null,
): InventoryEntryDraft {
  return {
    values: Object.fromEntries(
      columns
        .filter((column) => column.type !== 'Quantity')
        .map((column) => [column.id, entry?.values[column.id] ?? '']),
    ),
    quantity: entry?.quantity ?? null,
  }
}

function readQuantity(quantity: number | null | undefined): {
  readonly value: number | null
  readonly invalid: boolean
} {
  if (quantity === null || quantity === undefined) return { value: null, invalid: false }
  const invalid =
    !Number.isInteger(quantity) ||
    quantity < INVENTORY_MIN_QUANTITY ||
    quantity > INVENTORY_MAX_QUANTITY
  return { value: invalid ? null : quantity, invalid }
}

function readNumber(text: string): string | null {
  const normalized = text.replace(',', '.')
  const decimals = normalized.split('.')[1]?.length ?? 0
  return normalized.length <= INVENTORY_NUMBER_MAX_LENGTH &&
    NUMBER.test(normalized) &&
    decimals <= INVENTORY_NUMBER_MAX_DECIMALS &&
    Math.abs(Number(normalized)) <= INVENTORY_NUMBER_MAX_VALUE
    ? normalized
    : null
}

/**
 * Reads the entry dialog for the columns of its category: values are trimmed and a blank one is
 * left out, a number may use a comma for its decimals, has at most `INVENTORY_NUMBER_MAX_DECIMALS`
 * of them and stays within `INVENTORY_NUMBER_MAX_VALUE` either side of zero, and a quantity is a
 * whole number from `INVENTORY_MIN_QUANTITY` to `INVENTORY_MAX_QUANTITY`. A required column cannot
 * stay empty. Problems are keyed by column id.
 */
export function readInventoryEntryDraft(
  columns: readonly InventoryColumn[],
  draft: InventoryEntryDraft,
): FormReading<string, InventoryEntryInput> {
  const problems: Record<string, FormProblem> = {}
  const values: Record<string, string> = {}
  let quantity: number | null = null

  for (const column of columns) {
    if (column.type === 'Quantity') {
      const read = readQuantity(draft.quantity)
      quantity = read.value
      if (read.invalid) {
        problems[column.id] = 'pages.admin.inventoryCategory.form.problems.quantityInvalid'
      } else if (column.required && quantity === null) {
        problems[column.id] = 'pages.admin.inventoryCategory.form.problems.required'
      }
      continue
    }

    const text = (draft.values[column.id] ?? '').trim()
    if (!text) {
      if (column.required) {
        problems[column.id] = 'pages.admin.inventoryCategory.form.problems.required'
      }
      continue
    }

    const value = column.type === 'Number' ? readNumber(text) : text
    if (value === null) {
      problems[column.id] = 'pages.admin.inventoryCategory.form.problems.numberInvalid'
    } else {
      values[column.id] = value
    }
  }

  return Object.keys(problems).length > 0
    ? { problems, value: null }
    : { problems, value: { quantity, values } }
}
