import { describe, expect, it } from 'vitest'

import type { InventoryColumn } from '@/entities/inventory'
import { rawValue } from '@/entities/inventory/model/values'

function column(type: InventoryColumn['type']): InventoryColumn {
  return { id: 'value', name: 'Valor', type, required: false, options: [] }
}

function entry(value?: string, quantity: number | null = null) {
  return { id: 'e1', quantity, values: value === undefined ? {} : { value } }
}

describe('rawValue', () => {
  it('reads the quantity apart from the other values', () => {
    expect(rawValue(column('Quantity'), entry('x', 4))).toBe('4')
    expect(rawValue(column('Quantity'), entry('x'))).toBeUndefined()
    expect(rawValue(column('Text'), entry('x', 4))).toBe('x')
  })
})
