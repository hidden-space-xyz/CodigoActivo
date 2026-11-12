import { mutationOptions } from '@tanstack/vue-query'

import type { InventoryCategoryInput, InventoryEntryInput } from '../model/types'
import { inventoryKeys } from './queries'
import {
  addInventoryEntryRequest,
  createInventoryCategoryRequest,
  deleteInventoryCategoryRequest,
  deleteInventoryEntryRequest,
  updateInventoryCategoryRequest,
  updateInventoryEntryRequest,
} from './requests'

/** Mutation options of the inventory; each one refreshes every inventory query once it succeeds. */
export const inventoryMutations = {
  createCategory: () =>
    mutationOptions({
      mutationFn: (input: InventoryCategoryInput) => createInventoryCategoryRequest(input),
      meta: { invalidates: [inventoryKeys.all] },
    }),
  updateCategory: () =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: InventoryCategoryInput }) =>
        updateInventoryCategoryRequest(id, input),
      meta: { invalidates: [inventoryKeys.all] },
    }),
  removeCategory: () =>
    mutationOptions({
      mutationFn: (id: string) => deleteInventoryCategoryRequest(id),
      meta: { invalidates: [inventoryKeys.all] },
    }),
  /** Adds entries to the category `categoryId`. */
  addEntry: (categoryId: string) =>
    mutationOptions({
      mutationFn: (input: InventoryEntryInput) => addInventoryEntryRequest(categoryId, input),
      meta: { invalidates: [inventoryKeys.all] },
    }),
  /** Replaces entries of the category `categoryId`. */
  updateEntry: (categoryId: string) =>
    mutationOptions({
      mutationFn: ({ id, input }: { id: string; input: InventoryEntryInput }) =>
        updateInventoryEntryRequest(categoryId, id, input),
      meta: { invalidates: [inventoryKeys.all] },
    }),
  /** Deletes entries of the category `categoryId`. */
  removeEntry: (categoryId: string) =>
    mutationOptions({
      mutationFn: (id: string) => deleteInventoryEntryRequest(categoryId, id),
      meta: { invalidates: [inventoryKeys.all] },
    }),
}
