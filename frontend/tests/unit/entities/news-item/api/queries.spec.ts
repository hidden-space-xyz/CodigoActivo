import { describe, expect, it } from 'vitest'

import { newsKeys, newsList, newsPages, newsQueries } from '@/entities/news-item'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'
import { buildNewsItemResponse, buildNewsListItem } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'
import { createTestQueryClient } from '../../../../support/render'

describe('newsKeys', () => {
  it('nests every news query under one root', () => {
    expect(newsKeys.years()).toEqual(['news', 'years'])
    expect(newsKeys.pages('2026', 'robot')).toEqual(['news', 'pages', '2026', 'robot'])
    expect(newsKeys.home()).toEqual(['news', 'home'])
    expect(newsKeys.detail('a1')).toEqual(['news', 'detail', 'a1'])
    expect(newsKeys.list()).toEqual(['news', 'list'])
  })
})

describe('newsQueries', () => {
  it('loads the years, the home block and a whole news item', async () => {
    server.use(
      http.get('/api/news/years', () => HttpResponse.json([2026])),
      http.get('/api/news', () =>
        HttpResponse.json(paged([buildNewsListItem({ featured: true })])),
      ),
      http.get('/api/news/a1', () => HttpResponse.json(buildNewsItemResponse({ id: 'a1' }))),
      http.get('/api/news/missing', () => apiError(404)),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(newsQueries.years())).resolves.toEqual(['2026'])
    await expect(client.fetchQuery(newsQueries.home())).resolves.toMatchObject({
      featured: { id: 'news-item-1' },
      items: [],
    })
    await expect(client.fetchQuery(newsQueries.detail('a1'))).resolves.toMatchObject({ id: 'a1' })
    await expect(client.fetchQuery(newsQueries.detail('missing'))).resolves.toBeNull()
  })
})

describe('news sources', () => {
  it('pages the news items of a year matching a search under their own key', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/news', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildNewsListItem()], 3))
      }),
    )
    const source = newsPages('2026', 'robot')

    await expect(source.fetchPage(2, 10)).resolves.toMatchObject({ total: 3 })
    expect(source.queryKey).toEqual(newsKeys.pages('2026', 'robot'))
    expect(queryOf(urls[0] ?? '')).toEqual({
      year: '2026',
      search: 'robot',
      sort: '-createdAt',
      page: '2',
      pageSize: '10',
    })
  })

  it('pages the admin list under the list key', async () => {
    server.use(http.get('/api/news', () => HttpResponse.json(paged([buildNewsListItem()], 1))))

    await expect(newsList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 1 })
    expect(newsList.queryKey).toEqual(newsKeys.list())
  })
})
