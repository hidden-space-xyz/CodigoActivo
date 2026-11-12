import { describe, expect, it } from 'vitest'

import type { InventoryColumn, InventoryEntry } from '@/entities/inventory'
import { filterColumns, selectEntries } from '@/pages/admin/inventory-category/model/entry-table'

const COLUMNS: InventoryColumn[] = [
  { id: 'name', name: 'Nombre', type: 'Text', required: false, options: [] },
  {
    id: 'state',
    name: 'Estado',
    type: 'List',
    required: false,
    options: [
      { id: 'ok', name: 'Disponible', color: '#00AA00' },
      { id: 'ko', name: 'Averiado', color: '#AA0000' },
    ],
  },
  { id: 'ram', name: 'Memoria', type: 'Number', required: false, options: [] },
  { id: 'bought', name: 'Compra', type: 'Date', required: false, options: [] },
  { id: 'works', name: 'Funciona', type: 'Boolean', required: false, options: [] },
  { id: 'units', name: 'Cantidad', type: 'Quantity', required: false, options: [] },
]

function entry(id: string, values: Record<string, string>, quantity: number | null = null) {
  return { id, quantity, values } satisfies InventoryEntry
}

const ENTRIES = [
  entry(
    'e1',
    { name: 'Portátil Ñandú', state: 'ko', ram: '8', bought: '2024-03-05', works: 'false' },
    2,
  ),
  entry(
    'e2',
    { name: 'portatil 10', state: 'ok', ram: '16', bought: '2025-01-10', works: 'true' },
    1,
  ),
  entry('e3', { name: 'Portátil 2' }),
]

function ids(params: Record<string, unknown>): string[] {
  return selectEntries(COLUMNS, ENTRIES, params).items.map((item) => item.id)
}

describe('filterColumns', () => {
  it('filters numbers and quantities by number, days by range and the rest by text', () => {
    expect(filterColumns(COLUMNS)).toEqual({
      name: { type: 'text' },
      state: { type: 'text' },
      ram: { type: 'number' },
      bought: { type: 'dateRange' },
      works: { type: 'text' },
      units: { type: 'number' },
    })
  })
})

describe('selectEntries', () => {
  it('matches text ignoring case and accents, numbers, days, yes or no and options', () => {
    expect(ids({ name: 'PORTATIL' })).toEqual(['e1', 'e2', 'e3'])
    expect(ids({ name: 'nandu' })).toEqual(['e1'])
    expect(ids({ ram: 16 })).toEqual(['e2'])
    expect(ids({ units: 2 })).toEqual(['e1'])
    expect(ids({ boughtFrom: '2024-12-01' })).toEqual(['e2'])
    expect(ids({ boughtTo: '2024-12-01' })).toEqual(['e1'])
    expect(ids({ works: 'true' })).toEqual(['e2'])
    expect(ids({ state: 'ko', name: '' })).toEqual(['e1'])
  })

  it('sorts by the chosen column, lists by their options and empty values last', () => {
    expect(ids({ sort: 'name' })).toEqual(['e3', 'e2', 'e1'])
    expect(ids({ sort: '-ram' })).toEqual(['e2', 'e1', 'e3'])
    expect(ids({ sort: 'state' })).toEqual(['e2', 'e1', 'e3'])
    expect(ids({ sort: 'bought' })).toEqual(['e1', 'e2', 'e3'])
    expect(ids({ sort: '-works' })).toEqual(['e2', 'e1', 'e3'])
    expect(ids({ sort: 'units' })).toEqual(['e2', 'e1', 'e3'])
    expect(ids({ sort: 'unknown' })).toEqual(['e1', 'e2', 'e3'])
  })

  it('cuts the page asked for and counts every match', () => {
    expect(selectEntries(COLUMNS, ENTRIES, { page: 2, pageSize: 2, sort: 'name' })).toEqual({
      items: [ENTRIES[0]],
      total: 3,
    })
  })
})
