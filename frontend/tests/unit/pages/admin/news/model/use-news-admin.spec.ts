import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { NewsItemInput } from '@/entities/news-item'
import { useNewsAdmin } from '@/pages/admin/news/model/use-news-admin'

import { http, HttpResponse, noContent, paged, server } from '../../../../../support/server'
import { buildNewsItemResponse, buildNewsListItem, richText } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildNewsSummary } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

const INPUT: NewsItemInput = {
  title: 'Demo day',
  subtitle: 'Friday',
  description: richText('Details'),
  thumbnailId: 'thumb',
}

describe('useNewsAdmin', () => {
  it('requests the newest news items first', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildNewsListItem()]))
      }),
    )

    const { result } = await mountComposable(() => useNewsAdmin())

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-createdAt' })
  })

  it('features only a news item that is not featured yet', async () => {
    const featured: string[] = []
    server.use(
      http.get('/api/news', () => HttpResponse.json(paged([]))),
      http.patch('/api/news/:id/feature', ({ params }) => {
        featured.push(String(params.id))
        return noContent()
      }),
    )
    const { result } = await mountComposable(() => useNewsAdmin())

    result.feature(buildNewsSummary({ id: 'top', featured: true }))
    await flushPromises()
    result.feature(buildNewsSummary({ id: 'other', featured: false }))
    await expectNotification(t('pages.admin.news.toasts.featured'))

    expect(featured).toEqual(['other'])
  })

  it('creates a news item when none is edited and replaces the edited one', async () => {
    const calls: string[] = []
    server.use(
      http.get('/api/news', () => HttpResponse.json(paged([]))),
      http.get('/api/news/:id', () => HttpResponse.json(buildNewsItemResponse())),
      http.post('/api/news', () => {
        calls.push('POST')
        return HttpResponse.json(buildNewsItemResponse(), { status: 201 })
      }),
      http.put('/api/news/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildNewsItemResponse())
      }),
    )
    const { result } = await mountComposable(() => useNewsAdmin())

    result.dialog.openCreate()
    result.save(INPUT)
    await expectNotification(t('pages.admin.news.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit(buildNewsSummary())
    expect(result.dialog.editing.value?.description).toBe(richText('Ya puedes apuntarte.'))
    result.save(INPUT)
    await expectNotification(t('pages.admin.news.toasts.saved'))

    expect(calls).toEqual(['POST', 'PUT news-item-1'])
  })
})
