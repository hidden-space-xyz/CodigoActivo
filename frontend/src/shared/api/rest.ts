import { ApiError } from './http-client'

export const FEATURED_FIRST_SORT = '-featured,-createdAt'

export async function unwrapOrNull<T>(request: Promise<{ data: T }>): Promise<T | null> {
  try {
    return (await request).data
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

export function toPage<T>(response: { data: { items?: T[] | null; total?: number | null } }): {
  items: T[]
  total: number
} {
  return { items: response.data.items ?? [], total: response.data.total ?? 0 }
}
