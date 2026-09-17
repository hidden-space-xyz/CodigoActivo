import { describe, expect, it, vi } from 'vitest'

import { catalogQueryKeys, useCreateEventCategoryType } from '@/entities/catalog'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

describe('useCreateEventCategoryType', () => {
  it('creates the category and invalidates the category list and admin table', async () => {
    let body: unknown
    server.use(
      http.post('/api/events/categoryType', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ id: 'cat-1', name: 'Robótica', color: '#112233' })
      }),
    )
    const { result, queryClient } = await renderComposable(() => useCreateEventCategoryType())
    queryClient.setQueryData(catalogQueryKeys.eventCategoryTypes(), [])
    queryClient.setQueryData(catalogQueryKeys.eventCategoryTypesTable(), [])
    queryClient.setQueryData(catalogQueryKeys.userTypes(), [])

    const created = await result.mutateAsync({ name: 'Robótica', color: '#112233' })

    expect(created).toEqual({ id: 'cat-1', name: 'Robótica', color: '#112233' })
    expect(body).toEqual({ name: 'Robótica', color: '#112233' })
    const invalidated = (key: readonly unknown[]) =>
      queryClient.getQueryState(key)?.isInvalidated ?? false
    await vi.waitFor(() =>
      expect(invalidated(catalogQueryKeys.eventCategoryTypesTable())).toBe(true),
    )
    expect(invalidated(catalogQueryKeys.eventCategoryTypes())).toBe(true)
    expect(invalidated(catalogQueryKeys.userTypes())).toBe(false)
  })

  it('does not invalidate anything when the creation fails', async () => {
    server.use(
      http.post('/api/events/categoryType', () =>
        apiError(409, 'EventCategoryTypeNameAlreadyExists'),
      ),
    )
    const { result, queryClient } = await renderComposable(() => useCreateEventCategoryType())
    queryClient.setQueryData(catalogQueryKeys.eventCategoryTypes(), [])

    await expect(result.mutateAsync({ name: 'Duplicada' })).rejects.toMatchObject({ status: 409 })

    expect(queryClient.getQueryState(catalogQueryKeys.eventCategoryTypes())?.isInvalidated).toBe(
      false,
    )
  })
})
