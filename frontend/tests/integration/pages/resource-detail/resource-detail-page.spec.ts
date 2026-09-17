import { afterEach, describe, expect, it, vi } from 'vitest'

import { ResourceDetailPage } from '@/pages/resource-detail'
import type { ResourceResponse } from '@/shared/api/generated/models'

import { buildResourceResponse } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveResource(resource: ResourceResponse | 'not-found') {
  const requested: string[] = []
  server.use(
    http.get('/api/resources/:resourceId', ({ params }) => {
      requested.push(String(params.resourceId))
      return resource === 'not-found' ? apiError(404) : HttpResponse.json(resource)
    }),
  )
  return requested
}

async function renderPage() {
  const rendered = await renderWithProviders(ResourceDetailPage, {
    props: { resourceId: 'resource-1' },
    route: '/resources/resource-1',
  })
  await vi.waitFor(() =>
    expect(
      rendered.wrapper.find('.detail-state').exists() &&
        rendered.wrapper.find('.detail-state').text() === t('common.loading'),
    ).toBe(false),
  )
  return rendered
}

function metaContent(selector: string): string | null | undefined {
  return document.head.querySelector(selector)?.getAttribute('content')
}

afterEach(() => {
  window.location.hash = ''
})

describe('resource detail page', () => {
  it('shows a loading message while the resource loads', async () => {
    server.use(http.get('/api/resources/:resourceId', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(ResourceDetailPage, {
      props: { resourceId: 'resource-1' },
      route: '/resources/resource-1',
    })

    expect(wrapper.find('.detail-state').text()).toBe(t('common.loading'))
    expect(wrapper.find('a').attributes('href')).toBe('/resources')
  })

  it('renders the resource with its poster and description', async () => {
    const requested = serveResource(buildResourceResponse())

    const { wrapper } = await renderPage()

    expect(requested).toEqual(['resource-1'])
    expect(wrapper.find('h1').text()).toBe('Guía de Python')
    expect(wrapper.find('.detail__subtitle').text()).toBe('Primeros pasos')
    expect(wrapper.find('.detail__poster').attributes('src')).toBe(
      '/api/files/thumb-resource/content',
    )
    expect(wrapper.find('.rich-text').text()).toBe('Todo sobre Python.')
    await vi.waitFor(() => expect(document.title).toContain('Guía de Python'))
    expect(metaContent('meta[name="description"]')).toBe('Todo sobre Python.')
    expect(metaContent('meta[property="og:type"]')).toBe('article')
    expect(metaContent('meta[property="og:image"]')).toBe(
      `${window.location.origin}/api/files/thumb-resource/content`,
    )
  })

  it('shows a placeholder and uses the subtitle for SEO without description', async () => {
    serveResource(buildResourceResponse({ description: null, thumbnailId: '' }))

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail__body--muted').text()).toBe(
      t('pages.resourceDetail.noDescription'),
    )
    expect(wrapper.find('.detail__poster').exists()).toBe(false)
    await vi.waitFor(() => expect(metaContent('meta[name="description"]')).toBe('Primeros pasos'))
    expect(metaContent('meta[property="og:image"]')).toBe(`${window.location.origin}/og-image.png`)
  })

  it('falls back to the default description without description or subtitle', async () => {
    serveResource(buildResourceResponse({ description: '', subtitle: '' }))

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail__subtitle').exists()).toBe(false)
    await vi.waitFor(() => expect(document.title).toContain('Guía de Python'))
    expect(metaContent('meta[name="description"]')).toBe(t('seo.defaultDescription'))
  })

  it('shows the not-found message and marks the page as noindex', async () => {
    serveResource('not-found')

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-state').text()).toBe(t('pages.resourceDetail.notFound'))
    await vi.waitFor(() => expect(document.title).toContain(t('pages.resourceDetail.seoNotFound')))
    expect(metaContent('meta[name="robots"]')).toBe('noindex')
  })

  it('redirects link resources to their URL without indexing the page', async () => {
    serveResource(buildResourceResponse({ url: `${window.location.origin}/#guia-externa` }))

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-state').text()).toBe(t('pages.resourceDetail.redirecting'))
    await vi.waitFor(() => expect(window.location.hash).toBe('#guia-externa'))
    expect(metaContent('meta[name="robots"]')).toBe('noindex')
  })
})
