import { ApiError } from './http-client'

/** `sort` query value that lists featured items first, then newest first. */
export const FEATURED_FIRST_SORT = '-featured,-createdAt'

/** Unwraps the response data; `null` when the request fails with one of `statuses` (404). */
export async function unwrapOrNull<T>(
  request: Promise<{ data: T }>,
  statuses: readonly number[] = [404],
): Promise<T | null> {
  try {
    return (await request).data
  } catch (error) {
    if (error instanceof ApiError && statuses.includes(error.status)) return null
    throw error
  }
}

/** Normalizes a paged API response, defaulting missing `items` to `[]` and `total` to `0`. */
export function toPage<T>(response: { data: { items?: T[] | null; total?: number | null } }): {
  items: T[]
  total: number
} {
  return { items: response.data.items ?? [], total: response.data.total ?? 0 }
}
