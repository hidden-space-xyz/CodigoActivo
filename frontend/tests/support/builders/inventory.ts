import type {
  InventoryCategoryResponse,
  InventoryCategorySummaryResponse,
  InventoryEntryResponse,
} from '@/shared/api/generated/models'

/** Category as returned by `GET /api/inventory/categories`. */
export function buildInventorySummaryResponse(
  overrides: Partial<InventoryCategorySummaryResponse> = {},
): InventoryCategorySummaryResponse {
  return { id: 'category-1', name: 'Camisetas', entryCount: 2, total: 42, ...overrides }
}

/**
 * Category as returned by `GET /api/inventory/categories/{id}`: a list of sizes, a color as text
 * and a quantity.
 */
export function buildInventoryCategoryResponse(
  overrides: Partial<InventoryCategoryResponse> = {},
): InventoryCategoryResponse {
  return {
    id: 'category-1',
    name: 'Camisetas',
    entryCount: 2,
    total: 42,
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
      { id: 'color', name: 'Color', type: 'Text', required: false, options: [] },
      { id: 'quantity', name: 'Cantidad', type: 'Quantity', required: true, options: [] },
    ],
    ...overrides,
  }
}

/** Entry as returned by `GET /api/inventory/categories/{id}/entries`. */
export function buildInventoryEntryResponse(
  overrides: Partial<InventoryEntryResponse> = {},
): InventoryEntryResponse {
  return {
    id: 'entry-1',
    quantity: 30,
    values: { size: 'size-s', color: 'Negra' },
    ...overrides,
  }
}
