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

/** The rows of a paged API response, each turned into a model by `map`, and how many rows match. */
export function toPage<T, R>(
  response: { data: { items: T[]; total: number } },
  map: (item: T) => R,
): { items: R[]; total: number } {
  return { items: response.data.items.map(map), total: response.data.total }
}
