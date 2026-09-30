import { describe, expect, it, vi } from 'vitest'

import { useEventCategoriesAdmin } from '@/pages/admin/catalogs/model/use-event-categories-admin'

import { http, HttpResponse, noContent, paged, server } from '../../../../../support/server'
import { buildEventCategoryType } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { mountComposable, t } from '../../../../../support/render'

describe('useEventCategoriesAdmin', () => {
  it('lists the categories by name and creates or updates the one in the dialog', async () => {
    const urls: string[] = []
    const calls: string[] = []
    server.use(
      http.get('/api/events/categoryType', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventCategoryType()]))
      }),
      http.post('/api/events/categoryType', () => {
        calls.push('POST')
        return HttpResponse.json(buildEventCategoryType(), { status: 201 })
      }),
      http.put('/api/events/categoryType/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return noContent()
      }),
    )
    const { result } = await mountComposable(() => useEventCategoriesAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    result.dialog.openCreate()
    result.save({ name: 'Talk', color: '#00FF00' })
    await expectNotification(t('pages.admin.catalogs.eventCategories.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit({ id: 'category-1', name: 'Workshop', color: '#FF0000' })
    result.save({ name: 'Workshops', color: '#FF0000' })
    await expectNotification(t('pages.admin.catalogs.eventCategories.toasts.updated'))

    expect(calls).toEqual(['POST', 'PUT category-1'])
  })
})
