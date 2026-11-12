import { rawValue, type InventoryColumn, type InventoryEntry } from '@/entities/inventory'
import { LOCALE } from '@/shared/config'
import type { ServerTablePage } from '@/shared/lib/paging'

/** How a column of the table is filtered: by text, by number or by a range of days. */
type FilterKind = 'text' | 'number' | 'dateRange'

const DEFAULT_PAGE_SIZE = 25

const textCollator = new Intl.Collator(LOCALE, { sensitivity: 'base', numeric: true })

/** Kind of filter of each column, by column id, as `useServerTable` declares them. */
export function filterColumns(
  columns: readonly InventoryColumn[],
): Record<string, { readonly type: FilterKind }> {
  return Object.fromEntries(
    columns.map((column) => [
      column.id,
      {
        type:
          column.type === 'Number' || column.type === 'Quantity'
            ? 'number'
            : column.type === 'Date'
              ? 'dateRange'
              : 'text',
      },
    ]),
  )
}

function folded(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
}

function matches(
  column: InventoryColumn,
  entry: InventoryEntry,
  params: Readonly<Record<string, unknown>>,
): boolean {
  const value = rawValue(column, entry)
  if (column.type === 'Date') {
    const from = params[`${column.id}From`]
    const to = params[`${column.id}To`]
    if (typeof from !== 'string' && typeof to !== 'string') return true
    if (value === undefined) return false
    return (typeof from !== 'string' || value >= from) && (typeof to !== 'string' || value <= to)
  }

  const filter = params[column.id]
  if (typeof filter !== 'string' && typeof filter !== 'number') return true
  const term = String(filter)
  if (term === '') return true
  if (value === undefined) return false
  if (column.type === 'Number' || column.type === 'Quantity') return Number(value) === Number(term)
  if (column.type === 'Text' || column.type === 'LongText') {
    return folded(value).includes(folded(term))
  }
  return value === term
}

function compareValues(column: InventoryColumn, left: string, right: string): number {
  switch (column.type) {
    case 'Number':
    case 'Quantity':
      return Number(left) - Number(right)
    case 'List': {
      const position = (id: string) => column.options.findIndex((option) => option.id === id)
      return position(left) - position(right)
    }
    case 'Text':
    case 'LongText':
      return textCollator.compare(left, right)
    default:
      return left < right ? -1 : left > right ? 1 : 0
  }
}

function sorter(
  columns: readonly InventoryColumn[],
  sort: unknown,
): ((left: InventoryEntry, right: InventoryEntry) => number) | null {
  if (typeof sort !== 'string' || sort === '') return null
  const descending = sort.startsWith('-')
  const column = columns.find((candidate) => candidate.id === sort.replace(/^-/, ''))
  if (!column) return null
  return (left, right) => {
    const a = rawValue(column, left)
    const b = rawValue(column, right)
    if (a === undefined || b === undefined) {
      return a === b ? left.id.localeCompare(right.id, LOCALE) : a === undefined ? 1 : -1
    }
    const order = compareValues(column, a, b)
    return (descending ? -order : order) || left.id.localeCompare(right.id, LOCALE)
  }
}

/**
 * The page of entries a table asks for: those that match every column filter (text containing
 * the term ignoring case and accents, the same number, a day within the range, the same yes or no
 * or option), sorted by the column in `sort` (`-` for descending; a list by the order of its
 * options, entries without a value last) and cut to `page` and `pageSize`.
 */
export function selectEntries(
  columns: readonly InventoryColumn[],
  entries: readonly InventoryEntry[],
  params: Readonly<Record<string, unknown>>,
): ServerTablePage<InventoryEntry> {
  const kept = entries.filter((entry) => columns.every((column) => matches(column, entry, params)))
  const compare = sorter(columns, params.sort)
  const sorted = compare ? [...kept].sort(compare) : kept
  const pageSize = Number(params.pageSize) || DEFAULT_PAGE_SIZE
  const page = Math.max(1, Number(params.page) || 1)
  return { items: sorted.slice((page - 1) * pageSize, page * pageSize), total: kept.length }
}
