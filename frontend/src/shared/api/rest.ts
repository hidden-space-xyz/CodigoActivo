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

export function toPage<T>(response: {
  data: { items?: T[] | null; total?: number | null }
}): { items: T[]; total: number } {
  return { items: response.data.items ?? [], total: response.data.total ?? 0 }
}

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
