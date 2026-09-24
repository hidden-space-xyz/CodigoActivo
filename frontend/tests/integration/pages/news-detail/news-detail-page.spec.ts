import { describe, expect, it, vi } from 'vitest'

import { NewsDetailPage } from '@/pages/news-detail'
import type { NewsItemResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import { buildNewsItemResponse } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveNewsItem(newsItem: NewsItemResponse | 'not-found') {
  server.use(
    http.get('/api/news/:newsItemId', () =>
      newsItem === 'not-found' ? apiError(404) : HttpResponse.json(newsItem),
    ),
  )
}

async function renderPage() {
  const rendered = await renderWithProviders(NewsDetailPage, {
    props: { newsItemId: 'news-item-1' },
    route: '/news/news-item-1',
  })
  await vi.waitFor(() => expect(rendered.wrapper.text()).not.toContain(t('common.loading')))
  return rendered
}

function jsonLd(): Record<string, unknown> | null {
  const script = document.getElementById('ca-jsonld')
  return script ? (JSON.parse(script.textContent ?? '{}') as Record<string, unknown>) : null
}

describe('news detail page', () => {
  it('shows a loading message while the news item loads', async () => {
    server.use(http.get('/api/news/:newsItemId', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(NewsDetailPage, {
      props: { newsItemId: 'news-item-1' },
      route: '/news/news-item-1',
    })

    expect(wrapper.find('.detail-state').text()).toBe(t('common.loading'))
    expect(wrapper.find('a').attributes('href')).toBe('/news')
  })

  it('renders the news item and publishes a news article', async () => {
    serveNewsItem(buildNewsItemResponse())

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail__date').text()).toBe(formatDate('2026-02-01T10:00:00Z'))
    expect(wrapper.find('h1').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.detail__subtitle').text()).toBe('Nueva temporada')
    expect(wrapper.find('.detail__poster').attributes('src')).toBe(
      '/api/files/thumb-news-item/content',
    )
    expect(wrapper.find('.rich-text').text()).toBe('Ya puedes apuntarte.')

    await vi.waitFor(() => expect(document.title).toContain('Abrimos inscripciones'))
    expect(jsonLd()).toMatchObject({
      '@type': 'NewsArticle',
      headline: 'Abrimos inscripciones',
      url: `${window.location.origin}/news/news-item-1`,
      description: 'Ya puedes apuntarte.',
      image: `${window.location.origin}/api/files/thumb-news-item/content`,
      datePublished: '2026-02-01T10:00:00Z',
      dateModified: '2026-02-03T10:00:00Z',
      publisher: { logo: { url: `${window.location.origin}/apple-touch-icon.png` } },
    })
  })

  it('omits optional content and structured data fields for a sparse news item', async () => {
    serveNewsItem({ id: 'news-item-1', title: 'Solo título' })

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe('Solo título')
    expect(wrapper.find('.detail__date').exists()).toBe(false)
    expect(wrapper.find('.detail__subtitle').exists()).toBe(false)
    expect(wrapper.find('.detail__poster').exists()).toBe(false)
    expect(wrapper.find('.rich-text').exists()).toBe(false)

    await vi.waitFor(() => expect(jsonLd()?.headline).toBe('Solo título'))
    const data = jsonLd()
    for (const key of ['description', 'image', 'datePublished', 'dateModified']) {
      expect(data).not.toHaveProperty(key)
    }
    expect(document.head.querySelector('meta[name="description"]')?.getAttribute('content')).toBe(
      t('seo.defaultDescription'),
    )
  })

  it('uses the subtitle as description when the body is empty', async () => {
    serveNewsItem(buildNewsItemResponse({ description: '' }))

    await renderPage()

    await vi.waitFor(() => expect(jsonLd()?.description).toBe('Nueva temporada'))
  })

  it('shows the not-found message and marks the page as noindex', async () => {
    serveNewsItem('not-found')

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-state').text()).toBe(t('pages.newsDetail.notFound'))
    await vi.waitFor(() => expect(document.title).toContain(t('pages.newsDetail.notFoundTitle')))
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })
})
