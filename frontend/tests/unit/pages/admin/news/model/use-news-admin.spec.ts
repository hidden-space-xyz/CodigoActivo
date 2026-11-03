import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { useNewsAdmin } from '@/pages/admin/news/model/use-news-admin'

import { http, HttpResponse, noContent, paged, server } from '../../../../../support/server'
import { expectNotification } from '../../../../../support/dom'
import { buildNewsSummary } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

describe('useNewsAdmin', () => {
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
})
