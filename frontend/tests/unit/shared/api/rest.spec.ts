import { describe, expect, it } from 'vitest'

import { ApiError, FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'

describe('unwrapOrNull', () => {
  it('returns the response data on success', async () => {
    await expect(unwrapOrNull(Promise.resolve({ data: { id: 'a' } }))).resolves.toEqual({
      id: 'a',
    })
  })

  it('returns null for a 404 by default', async () => {
    await expect(unwrapOrNull(Promise.reject(new ApiError(404, 'Missing')))).resolves.toBeNull()
  })

  it('returns null for any of the configured statuses', async () => {
    await expect(
      unwrapOrNull(Promise.reject(new ApiError(403, 'Forbidden')), [401, 403]),
    ).resolves.toBeNull()
  })

  it('rethrows API errors with other statuses', async () => {
    const error = new ApiError(500, 'Boom')

    await expect(unwrapOrNull(Promise.reject(error))).rejects.toBe(error)
  })

  it('rethrows errors that are not API errors', async () => {
    const error = new TypeError('Network down')

    await expect(unwrapOrNull(Promise.reject(error))).rejects.toBe(error)
  })
})

describe('toPage', () => {
  it('returns items and total from the response', () => {
    expect(toPage({ data: { items: [1, 2], total: 7 } })).toEqual({ items: [1, 2], total: 7 })
  })

  it('defaults missing items and total', () => {
    expect(toPage<number>({ data: { items: null, total: null } })).toEqual({ items: [], total: 0 })
    expect(toPage<number>({ data: {} })).toEqual({ items: [], total: 0 })
  })
})

describe('FEATURED_FIRST_SORT', () => {
  it('sorts featured items first and then newest first', () => {
    expect(FEATURED_FIRST_SORT).toBe('-featured,-createdAt')
  })
})
