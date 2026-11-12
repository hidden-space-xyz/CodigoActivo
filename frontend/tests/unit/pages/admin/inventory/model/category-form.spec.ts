import { describe, expect, it } from 'vitest'

import type { InventoryCategory, InventoryColumn } from '@/entities/inventory'
import {
  columnTypeLabelKey,
  columnUsage,
  draftColumnUsage,
  INVENTORY_COLUMN_TYPES,
  moveItem,
  newColumnDraft,
  newOptionDraft,
  quantityTakenElsewhere,
  readInventoryCategoryDraft,
  toInventoryCategoryDraft,
  type EditedInventoryCategory,
  type InventoryCategoryDraft,
} from '@/pages/admin/inventory/model/category-form'

const SIZE: InventoryColumn = {
  id: 'size',
  name: 'Talla',
  type: 'List',
  required: true,
  options: [
    { id: 'size-s', name: 'S', color: '#112233' },
    { id: 'size-m', name: 'M', color: '#445566' },
  ],
}
const QUANTITY: InventoryColumn = {
  id: 'quantity',
  name: 'Cantidad',
  type: 'Quantity',
  required: false,
  options: [],
}
const CATEGORY: InventoryCategory = {
  id: 'category-1',
  name: 'Camisetas',
  entryCount: 2,
  total: 12,
  columns: [SIZE, QUANTITY],
}

function draft(overrides: Partial<InventoryCategoryDraft> = {}): InventoryCategoryDraft {
  return { ...toInventoryCategoryDraft(CATEGORY), ...overrides }
}

describe('toInventoryCategoryDraft', () => {
  it('starts a new category with one blank text column', () => {
    const blank = toInventoryCategoryDraft(null)
    expect(blank.name).toBe('')
    expect(blank.columns).toEqual([
      expect.objectContaining({ id: null, name: '', type: 'Text', required: false, options: [] }),
    ])
  })

  it('fills the columns and options of an edited category, keyed by their ids', () => {
    expect(draft().columns[0]).toEqual({
      key: 'size',
      id: 'size',
      name: 'Talla',
      type: 'List',
      required: true,
      options: [
        { key: 'size-s', id: 'size-s', name: 'S', color: '#112233' },
        { key: 'size-m', id: 'size-m', name: 'M', color: '#445566' },
      ],
    })
  })

  it('gives every new column and option a key of its own', () => {
    expect(newColumnDraft().key).not.toBe(newColumnDraft().key)
    expect(newOptionDraft()).toEqual(expect.objectContaining({ id: null, color: '#6366F1' }))
  })
})

describe('readInventoryCategoryDraft', () => {
  it('trims the names and sends the options of list columns only', () => {
    const value = readInventoryCategoryDraft(
      draft({
        name: ' Camisetas 2026 ',
        columns: [
          { ...draft().columns[0]!, name: ' Talla ' },
          {
            key: 'k1',
            id: null,
            name: 'Notas',
            type: 'LongText',
            required: false,
            options: [{ key: 'o1', id: null, name: 'x', color: '#000000' }],
          },
        ],
      }),
    )

    expect(value).toEqual({
      problems: {},
      value: {
        name: 'Camisetas 2026',
        columns: [
          {
            id: 'size',
            name: 'Talla',
            type: 'List',
            required: true,
            options: [
              { id: 'size-s', name: 'S', color: '#112233' },
              { id: 'size-m', name: 'M', color: '#445566' },
            ],
          },
          { id: null, name: 'Notas', type: 'LongText', required: false, options: [] },
        ],
      },
    })
  })

  it('refuses blank and repeated names, empty lists and a second quantity column', () => {
    const reading = readInventoryCategoryDraft({
      name: ' ',
      columns: [
        { key: 'a', id: null, name: 'Tipo', type: 'Quantity', required: false, options: [] },
        { key: 'b', id: null, name: ' tipo ', type: 'Quantity', required: false, options: [] },
        { key: 'c', id: null, name: '', type: 'List', required: false, options: [] },
        {
          key: 'd',
          id: null,
          name: 'Estado',
          type: 'List',
          required: false,
          options: [
            { key: 'o1', id: null, name: 'Bien', color: '#000000' },
            { key: 'o2', id: null, name: 'BIEN', color: '#000000' },
            { key: 'o3', id: null, name: ' ', color: '#000000' },
          ],
        },
      ],
    })

    expect(reading).toEqual({
      value: null,
      problems: {
        name: 'pages.admin.inventory.form.problems.nameRequired',
        'b.name': 'pages.admin.inventory.form.problems.columnNameRepeated',
        'b.type': 'pages.admin.inventory.form.problems.quantityRepeated',
        'c.name': 'pages.admin.inventory.form.problems.columnNameRequired',
        'c.options': 'pages.admin.inventory.form.problems.optionsRequired',
        'o2.name': 'pages.admin.inventory.form.problems.optionNameRepeated',
        'o3.name': 'pages.admin.inventory.form.problems.optionNameRequired',
      },
    })
    expect(readInventoryCategoryDraft({ name: 'Vacía', columns: [] }).problems).toEqual({
      columns: true,
    })
  })
})

describe('column usage', () => {
  const entries = [
    { id: 'e1', quantity: 5, values: { size: 'size-s' } },
    { id: 'e2', quantity: null, values: {} },
  ]

  it('counts the values, spots the empty entries and collects the chosen options', () => {
    expect(columnUsage(SIZE, entries)).toEqual({
      filled: 1,
      empty: true,
      chosen: new Set(['size-s']),
    })
    expect(columnUsage(QUANTITY, entries)).toEqual({ filled: 1, empty: true, chosen: new Set() })
  })

  it('reads a draft column from the edited category, or as empty everywhere when new', () => {
    const edited: EditedInventoryCategory = {
      ...CATEGORY,
      usage: { size: columnUsage(SIZE, entries) },
    }
    const [size] = draft().columns

    expect(draftColumnUsage(size!, edited).filled).toBe(1)
    expect(draftColumnUsage(newColumnDraft(), edited)).toEqual({
      filled: 0,
      empty: true,
      chosen: new Set(),
    })
    expect(draftColumnUsage(newColumnDraft(), null).empty).toBe(false)
  })

  it('tells whether another column already counts the quantity', () => {
    const current = draft()
    expect(quantityTakenElsewhere(current, current.columns[0]!)).toBe(true)
    expect(quantityTakenElsewhere(current, current.columns[1]!)).toBe(false)
  })
})

describe('moveItem', () => {
  it('moves an item one place and never past an end', () => {
    const items = ['a', 'b', 'c']
    moveItem(items, 0, 1)
    expect(items).toEqual(['b', 'a', 'c'])
    moveItem(items, 0, -1)
    moveItem(items, 2, 1)
    expect(items).toEqual(['b', 'a', 'c'])
  })
})

describe('column types', () => {
  it('lists every type with its label key', () => {
    expect(INVENTORY_COLUMN_TYPES).toHaveLength(7)
    expect(columnTypeLabelKey('Quantity')).toBe('pages.admin.inventory.form.types.Quantity')
  })
})
