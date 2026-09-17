import { describe, expect, it, vi } from 'vitest'

import { AnnouncementDetailPage } from '@/pages/announcement-detail'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import { buildAnnouncementResponse } from '../../../support/fixtures/public-dashboard/builders'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveAnnouncement(announcement: AnnouncementResponse | 'not-found') {
  server.use(
    http.get('/api/announcements/:announcementId', () =>
      announcement === 'not-found' ? apiError(404) : HttpResponse.json(announcement),
    ),
  )
}

async function renderPage() {
  const rendered = await renderWithProviders(AnnouncementDetailPage, {
    props: { announcementId: 'announcement-1' },
    route: '/announcements/announcement-1',
  })
  await vi.waitFor(() => expect(rendered.wrapper.text()).not.toContain(t('common.loading')))
  return rendered
}

function jsonLd(): Record<string, unknown> | null {
  const script = document.getElementById('ca-jsonld')
  return script ? (JSON.parse(script.textContent ?? '{}') as Record<string, unknown>) : null
}

describe('announcement detail page', () => {
  it('shows a loading message while the announcement loads', async () => {
    server.use(
      http.get('/api/announcements/:announcementId', () => new Promise<never>(() => undefined)),
    )

    const { wrapper } = await renderWithProviders(AnnouncementDetailPage, {
      props: { announcementId: 'announcement-1' },
      route: '/announcements/announcement-1',
    })

    expect(wrapper.find('.detail-state').text()).toBe(t('common.loading'))
    expect(wrapper.find('a').attributes('href')).toBe('/announcements')
  })

  it('renders the announcement and publishes a news article', async () => {
    serveAnnouncement(buildAnnouncementResponse())

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail__date').text()).toBe(formatDate('2026-02-01T10:00:00Z'))
    expect(wrapper.find('h1').text()).toBe('Abrimos inscripciones')
    expect(wrapper.find('.detail__subtitle').text()).toBe('Nueva temporada')
    expect(wrapper.find('.detail__poster').attributes('src')).toBe(
      '/api/files/thumb-announcement/content',
    )
    expect(wrapper.find('.rich-text').text()).toBe('Ya puedes apuntarte.')

    await vi.waitFor(() => expect(document.title).toContain('Abrimos inscripciones'))
    expect(jsonLd()).toMatchObject({
      '@type': 'NewsArticle',
      headline: 'Abrimos inscripciones',
      url: `${window.location.origin}/announcements/announcement-1`,
      description: 'Ya puedes apuntarte.',
      image: `${window.location.origin}/api/files/thumb-announcement/content`,
      datePublished: '2026-02-01T10:00:00Z',
      dateModified: '2026-02-03T10:00:00Z',
      publisher: { logo: { url: `${window.location.origin}/apple-touch-icon.png` } },
    })
  })

  it('omits optional content and structured data fields for a sparse announcement', async () => {
    serveAnnouncement({ id: 'announcement-1', title: 'Solo título' })

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
    serveAnnouncement(buildAnnouncementResponse({ description: '' }))

    await renderPage()

    await vi.waitFor(() => expect(jsonLd()?.description).toBe('Nueva temporada'))
  })

  it('shows the not-found message and marks the page as noindex', async () => {
    serveAnnouncement('not-found')

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-state').text()).toBe(t('pages.announcementDetail.notFound'))
    await vi.waitFor(() =>
      expect(document.title).toContain(t('pages.announcementDetail.notFoundTitle')),
    )
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })
})
