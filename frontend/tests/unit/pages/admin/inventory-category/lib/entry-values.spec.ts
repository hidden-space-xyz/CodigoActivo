import { describe, expect, it } from 'vitest'

import type { InventoryColumn } from '@/entities/inventory'
import {
  chosenOption,
  displayValue,
  entryLabel,
} from '@/pages/admin/inventory-category/lib/entry-values'

import { t } from '../../../../../support/render'

function column(type: InventoryColumn['type']): InventoryColumn {
  return {
    id: 'value',
    name: 'Valor',
    type,
    required: false,
    options: [{ id: 'ok', name: 'Disponible', color: '#00AA00' }],
  }
}

function entry(value?: string, quantity: number | null = null) {
  return { id: 'e1', quantity, values: value === undefined ? {} : { value } }
}

describe('entry values', () => {
  it('finds the option an entry chose', () => {
    expect(chosenOption(column('List'), entry('ok'))?.name).toBe('Disponible')
  })

  it('shows each value as a person reads it', () => {
    expect(displayValue(column('Number'), entry('1234.5'), t)).toBe('1234,5')
    expect(displayValue(column('Number'), entry('9999999999999.99'), t)).toBe(
      '9.999.999.999.999,99',
    )
    expect(displayValue(column('Quantity'), entry(undefined, 1500), t)).toBe('1500')
    expect(displayValue(column('Date'), entry('2024-03-05'), t)).toBe('5 mar 2024')
    expect(displayValue(column('Boolean'), entry('true'), t)).toBe('Sí')
    expect(displayValue(column('Boolean'), entry('false'), t)).toBe('No')
    expect(displayValue(column('List'), entry('ok'), t)).toBe('Disponible')
    expect(displayValue(column('List'), entry('gone'), t)).toBe('')
    expect(displayValue(column('Text'), entry(), t)).toBe('')
    expect(displayValue(column('LongText'), entry('Una\nnota'), t)).toBe('Una\nnota')
  })

  it('names an entry by its first value, or as «esta fila»', () => {
    const columns = [column('Text'), { ...column('Number'), id: 'other' }]
    expect(entryLabel(columns, entry('mBot'), t)).toBe('mBot')
    expect(entryLabel(columns, entry(), t)).toBe('esta fila')
  })
})
