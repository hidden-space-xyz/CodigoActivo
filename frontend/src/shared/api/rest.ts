import { ApiError } from './http-client'

/**
 * Unwraps a generated read request, returning `null` when the resource does not exist (404)
 * instead of throwing. Detail/edit screens rely on this to render a "not found" state.
 */
export async function unwrapOrNull<T>(request: Promise<{ data: T }>): Promise<T | null> {
  try {
    return (await request).data
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}

/** Unwraps a generated `PagedResult` response into a non-nullable `{ items, total }` page. */
export function toPage<T>(response: {
  data: { items?: T[] | null; total?: number | null }
}): { items: T[]; total: number } {
  return { items: response.data.items ?? [], total: response.data.total ?? 0 }
}

/**
 * Fetches every page of a bounded list read (e.g. one year's archive) and concatenates the items,
 * so a result set larger than a single page is never silently truncated. Pages after the first
 * are fetched in parallel once the total is known.
 */
export async function fetchAllPages<T>(
  fetchPage: (page: number, pageSize: number) => Promise<{ items: T[]; total: number }>,
  pageSize = 100,
): Promise<T[]> {
  const first = await fetchPage(1, pageSize)
  const pageCount = Math.ceil(first.total / pageSize)
  const rest = await Promise.all(
    Array.from({ length: Math.max(0, pageCount - 1) }, (_, i) => fetchPage(i + 2, pageSize)),
  )
  return [...first.items, ...rest.flatMap((page) => page.items)]
}
