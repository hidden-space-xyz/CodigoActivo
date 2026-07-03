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

/**
 * Fetches every page of a bounded list read (e.g. one year's archive) and concatenates the items,
 * so a result set larger than a single page is never silently truncated.
 */
export async function fetchAllPages<T>(
  fetchPage: (page: number, pageSize: number) => Promise<{ items: T[]; total: number }>,
  pageSize = 100,
): Promise<T[]> {
  const first = await fetchPage(1, pageSize)
  const items = [...first.items]
  const pageCount = Math.ceil(first.total / pageSize)
  for (let page = 2; page <= pageCount; page++) {
    const next = await fetchPage(page, pageSize)
    items.push(...next.items)
  }
  return items
}
