import { describe, expect, it } from 'vitest'

import type { InventoryColumn } from '@/entities/inventory'
import {
  readInventoryEntryDraft,
  toInventoryEntryDraft,
} from '@/pages/admin/inventory-category/model/entry-form'

const COLUMNS: InventoryColumn[] = [
  { id: 'name', name: 'Nombre', type: 'Text', required: true, options: [] },
  { id: 'notes', name: 'Notas', type: 'LongText', required: false, options: [] },
  { id: 'ram', name: 'Memoria', type: 'Number', required: false, options: [] },
  { id: 'units', name: 'Cantidad', type: 'Quantity', required: true, options: [] },
]

describe('toInventoryEntryDraft', () => {
  it('starts blank, or with the values and quantity of the edited entry', () => {
    expect(toInventoryEntryDraft(COLUMNS, null)).toEqual({
      values: { name: '', notes: '', ram: '' },
      quantity: null,
    })
    expect(
      toInventoryEntryDraft(COLUMNS, { id: 'e1', quantity: 3, values: { name: 'mBot' } }),
    ).toEqual({ values: { name: 'mBot', notes: '', ram: '' }, quantity: 3 })
  })
})

describe('readInventoryEntryDraft', () => {
  it('trims the values, leaves blanks out and writes decimals with a dot', () => {
    expect(
      readInventoryEntryDraft(COLUMNS, {
        values: { name: ' mBot ', notes: '  ', ram: ' 12,5 ' },
        quantity: 0,
      }),
    ).toEqual({ problems: {}, value: { quantity: 0, values: { name: 'mBot', ram: '12.5' } } })
  })

  it('refuses empty required columns, numbers it cannot read and odd quantities', () => {
    expect(
      readInventoryEntryDraft(COLUMNS, {
        values: { name: null, notes: undefined, ram: '12 GB' },
        quantity: undefined,
      }),
    ).toEqual({
      value: null,
      problems: {
        name: 'pages.admin.inventoryCategory.form.problems.required',
        ram: 'pages.admin.inventoryCategory.form.problems.numberInvalid',
        units: 'pages.admin.inventoryCategory.form.problems.required',
      },
    })
    expect(
      readInventoryEntryDraft(COLUMNS, { values: { name: 'mBot' }, quantity: 1.5 }).problems,
    ).toEqual({ units: 'pages.admin.inventoryCategory.form.problems.quantityInvalid' })
  })

  it('keeps numbers within two decimals and the largest value either side of zero', () => {
    const ram = (value: string) =>
      readInventoryEntryDraft(COLUMNS, { values: { name: 'mBot', ram: value }, quantity: 1 })

    expect(ram('-9999999999999,99').value?.values.ram).toBe('-9999999999999.99')
    expect(ram('0.5').value?.values.ram).toBe('0.5')
    expect(ram('1.234').problems).toEqual({
      ram: 'pages.admin.inventoryCategory.form.problems.numberInvalid',
    })
    expect(ram('10000000000000').problems).toEqual({
      ram: 'pages.admin.inventoryCategory.form.problems.numberInvalid',
    })
  })
})
