import {
  rawValue,
  type InventoryCategory,
  type InventoryCategoryInput,
  type InventoryColumn,
  type InventoryColumnType,
  type InventoryEntry,
} from '@/entities/inventory'
import type { TranslationKey } from '@/shared/i18n'
import { DEFAULT_TAG_COLOR, toHexColor } from '@/shared/lib/color'
import type { FormProblem, FormReading } from '@/shared/lib/form'

/** Kinds of column in the order the type selector lists them. */
export const INVENTORY_COLUMN_TYPES: readonly InventoryColumnType[] = [
  'Text',
  'LongText',
  'Number',
  'Date',
  'Boolean',
  'List',
  'Quantity',
]

/** Translation key of the name of a kind of column. */
export function columnTypeLabelKey(type: InventoryColumnType): TranslationKey {
  return `pages.admin.inventory.form.types.${type}`
}

/** An option of a list column as the dialog edits it; `key` tells rows apart while editing. */
export interface InventoryOptionDraft {
  key: string
  id: string | null
  name: string
  color: string
}

/** A column as the dialog edits it; a list keeps its options while another type is chosen. */
export interface InventoryColumnDraft {
  key: string
  id: string | null
  name: string
  type: InventoryColumnType
  required: boolean
  options: InventoryOptionDraft[]
}

/** What the category dialog binds its inputs to. */
export interface InventoryCategoryDraft {
  name: string
  columns: InventoryColumnDraft[]
}

/** What the entries of a category say about one of its columns. */
export interface InventoryColumnUsage {
  /** How many entries have a value in it; while any does, its type cannot change. */
  readonly filled: number
  /** Some entry has no value in it, so it cannot become required. */
  readonly empty: boolean
  /** Options some entry chose, which cannot be removed. */
  readonly chosen: ReadonlySet<string>
}

/** A category being edited, with what its entries say about each column, by column id. */
export interface EditedInventoryCategory extends InventoryCategory {
  readonly usage: Readonly<Record<string, InventoryColumnUsage>>
}

let lastKey = 0

function newKey(): string {
  lastKey += 1
  return `new-${lastKey}`
}

/** A new option with the default color. */
export function newOptionDraft(): InventoryOptionDraft {
  return { key: newKey(), id: null, name: '', color: DEFAULT_TAG_COLOR }
}

/** A new text column. */
export function newColumnDraft(): InventoryColumnDraft {
  return { key: newKey(), id: null, name: '', type: 'Text', required: false, options: [] }
}

function toColumnDraft(column: InventoryColumn): InventoryColumnDraft {
  return {
    key: column.id,
    id: column.id,
    name: column.name,
    type: column.type,
    required: column.required,
    options: column.options.map((option) => ({ key: option.id, ...option })),
  }
}

/** A draft with one text column, or one filled with the category being edited. */
export function toInventoryCategoryDraft(
  category: InventoryCategory | null,
): InventoryCategoryDraft {
  return category
    ? { name: category.name, columns: category.columns.map(toColumnDraft) }
    : { name: '', columns: [newColumnDraft()] }
}

/** What the entries say about a column: how many have a value, whether any has none, the options. */
export function columnUsage(
  column: InventoryColumn,
  entries: readonly InventoryEntry[],
): InventoryColumnUsage {
  const values = entries.map((entry) => rawValue(column, entry))
  return {
    filled: values.filter((value) => value !== undefined).length,
    empty: values.some((value) => value === undefined),
    chosen: new Set(column.type === 'List' ? values.filter((value) => value !== undefined) : []),
  }
}

/** What the entries say about a column of the draft; a new column is empty in every entry. */
export function draftColumnUsage(
  column: InventoryColumnDraft,
  category: EditedInventoryCategory | null,
): InventoryColumnUsage {
  const known = column.id ? category?.usage[column.id] : undefined
  return known ?? { filled: 0, empty: (category?.entryCount ?? 0) > 0, chosen: new Set() }
}

/** Tells whether a column other than `column` already counts the quantity. */
export function quantityTakenElsewhere(
  draft: InventoryCategoryDraft,
  column: InventoryColumnDraft,
): boolean {
  return draft.columns.some((other) => other.key !== column.key && other.type === 'Quantity')
}

/** Moves the item at `index` one place up (`-1`) or down (`1`); nothing moves past an end. */
export function moveItem<T>(items: T[], index: number, step: -1 | 1): void {
  const target = index + step
  if (target < 0 || target >= items.length) return
  const [item] = items.splice(index, 1)
  if (item !== undefined) items.splice(target, 0, item)
}

function repeatedNames(items: readonly { key: string; name: string }[]): Set<string> {
  const seen = new Map<string, string>()
  const repeated = new Set<string>()
  for (const item of items) {
    const name = item.name.trim().toLowerCase()
    if (!name) continue
    if (seen.has(name)) repeated.add(item.key)
    else seen.set(name, item.key)
  }
  return repeated
}

function readOptions(column: InventoryColumnDraft, problems: Record<string, FormProblem>): void {
  if (column.options.length === 0) {
    problems[`${column.key}.options`] = 'pages.admin.inventory.form.problems.optionsRequired'
  }
  const repeated = repeatedNames(column.options)
  for (const option of column.options) {
    if (!option.name.trim()) {
      problems[`${option.key}.name`] = 'pages.admin.inventory.form.problems.optionNameRequired'
    } else if (repeated.has(option.key)) {
      problems[`${option.key}.name`] = 'pages.admin.inventory.form.problems.optionNameRepeated'
    }
  }
}

/**
 * Reads the category dialog: the category and every column need a name, column names and the
 * option names of each list are unique ignoring letter case, a list has at least one option and
 * only one column counts the quantity. Problems are keyed by `name`, `<column key>.name`,
 * `<column key>.type`, `<column key>.options` and `<option key>.name`. Only list columns send
 * their options, with the color as `#` hex.
 */
export function readInventoryCategoryDraft(
  draft: InventoryCategoryDraft,
): FormReading<string, InventoryCategoryInput> {
  const problems: Record<string, FormProblem> = {}
  const name = draft.name.trim()
  if (!name) problems.name = 'pages.admin.inventory.form.problems.nameRequired'
  if (draft.columns.length === 0) problems.columns = true

  const repeated = repeatedNames(draft.columns)
  let counted = false
  for (const column of draft.columns) {
    if (!column.name.trim()) {
      problems[`${column.key}.name`] = 'pages.admin.inventory.form.problems.columnNameRequired'
    } else if (repeated.has(column.key)) {
      problems[`${column.key}.name`] = 'pages.admin.inventory.form.problems.columnNameRepeated'
    }
    if (column.type === 'Quantity') {
      if (counted) {
        problems[`${column.key}.type`] = 'pages.admin.inventory.form.problems.quantityRepeated'
      }
      counted = true
    }
    if (column.type === 'List') readOptions(column, problems)
  }

  if (Object.keys(problems).length > 0) return { problems, value: null }
  return {
    problems,
    value: {
      name,
      columns: draft.columns.map((column) => ({
        id: column.id,
        name: column.name.trim(),
        type: column.type,
        required: column.required,
        options:
          column.type === 'List'
            ? column.options.map((option) => ({
                id: option.id,
                name: option.name.trim(),
                color: toHexColor(option.color),
              }))
            : [],
      })),
    },
  }
}
