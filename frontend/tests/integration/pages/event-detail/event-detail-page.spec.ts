import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { AuthUser } from '@/entities/session/model/types'
import { EventDetailPage } from '@/pages/event-detail'
import EventActivitiesTimeline from '@/pages/event-detail/ui/EventActivitiesTimeline.vue'
import type { EventResponse } from '@/shared/api/generated/models'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib'

import {
  buildEventResponse,
  FAR_FUTURE,
  LONG_AGO,
  omit,
} from '../../../support/fixtures/public-dashboard/builders'
import { serveSignupApi } from '../../../support/fixtures/public-dashboard/signup-api'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveEvent(event: EventResponse | 'not-found') {
  server.use(
    http.get('/api/events/:eventId', () =>
      event === 'not-found' ? apiError(404) : HttpResponse.json(event),
    ),
  )
}

async function renderPage(user?: Partial<AuthUser>) {
  const rendered = await renderWithProviders(EventDetailPage, {
    props: { eventId: 'event-1' },
    route: '/events/event-1',
    ...(user ? { user } : {}),
  })
  await vi.waitFor(() => expect(rendered.wrapper.text()).not.toContain(t('common.loading')))
  return rendered
}

function jsonLd(): Record<string, unknown> | null {
  const script = document.getElementById('ca-jsonld')
  return script ? (JSON.parse(script.textContent ?? '{}') as Record<string, unknown>) : null
}

function metaContent(selector: string): string | null | undefined {
  return document.head.querySelector(selector)?.getAttribute('content')
}

describe('event detail page', () => {
  it('shows a loading message while the event loads', async () => {
    server.use(http.get('/api/events/:eventId', () => new Promise<never>(() => undefined)))

    const { wrapper } = await renderWithProviders(EventDetailPage, {
      props: { eventId: 'event-1' },
      route: '/events/event-1',
    })

    expect(wrapper.find('.detail-state').text()).toBe(t('common.loading'))
    expect(wrapper.find('a').attributes('href')).toBe('/events')
  })

  it('renders the event information panel and poster', async () => {
    const event = buildEventResponse()
    serveEvent(event)

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe('Hackathon de primavera')
    expect(wrapper.find('.detail-head__slogan').text()).toBe(
      t('pages.eventDetail.slogan', { subtitle: 'Programa tu futuro' }),
    )
    expect(wrapper.find('.detail-head__cats').text()).toContain('Programación')
    expect(wrapper.find('.detail-body__poster').attributes('src')).toBe(
      '/api/files/thumb-event/content',
    )
    expect(wrapper.find('.rich-text').text()).toBe('Un fin de semana de código.')

    const rows = wrapper.findAll('.detail-panel__row').map((row) => ({
      label: row.find('dt').text(),
      value: row.find('dd').text(),
    }))
    expect(rows).toEqual([
      {
        label: t('pages.eventDetail.info.date'),
        value: formatDateRange(event.eventStartsAt, event.eventEndsAt),
      },
      {
        label: t('pages.eventDetail.info.signup'),
        value: formatDateTimeRange(LONG_AGO, FAR_FUTURE),
      },
      { label: t('common.status'), value: t('entities.event.status.signupOpen') },
    ])
  })

  it('publishes the event SEO with structured data', async () => {
    serveEvent(buildEventResponse())

    await renderPage()

    await vi.waitFor(() => expect(document.title).toContain('Hackathon de primavera'))
    expect(metaContent('meta[name="description"]')).toBe('Un fin de semana de código.')
    expect(metaContent('meta[property="og:type"]')).toBe('article')
    expect(metaContent('meta[property="og:image"]')).toBe(
      `${window.location.origin}/api/files/thumb-event/content`,
    )
    expect(jsonLd()).toMatchObject({
      '@type': 'Event',
      name: 'Hackathon de primavera',
      url: `${window.location.origin}/events/event-1`,
      startDate: '2099-06-10T09:00:00Z',
      endDate: '2099-06-11T18:00:00Z',
      description: 'Un fin de semana de código.',
      image: `${window.location.origin}/api/files/thumb-event/content`,
    })
  })

  it('falls back to the subtitle and omits optional data for a sparse event', async () => {
    serveEvent(
      omit(
        buildEventResponse({ subtitle: '', description: '', thumbnailId: '', categories: [] }),
        'eventEndsAt',
      ),
    )

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-head__slogan').exists()).toBe(false)
    expect(wrapper.find('.detail-head__cats').exists()).toBe(false)
    expect(wrapper.find('.detail-body__poster').exists()).toBe(false)
    expect(wrapper.find('.detail-body__p--muted').text()).toBe(t('pages.eventDetail.noDescription'))
    await vi.waitFor(() => expect(jsonLd()).not.toBeNull())
    const data = jsonLd()
    expect(data).not.toHaveProperty('description')
    expect(data).not.toHaveProperty('image')
    expect(data).not.toHaveProperty('endDate')
  })

  it('uses the subtitle as description and skips structured data without a start date', async () => {
    serveEvent(omit(buildEventResponse({ description: '' }), 'eventStartsAt'))

    await renderPage()

    await vi.waitFor(() =>
      expect(metaContent('meta[name="description"]')).toBe('Programa tu futuro'),
    )
    expect(jsonLd()).toBeNull()
  })

  it('shows the not-found message and marks the page as noindex', async () => {
    serveEvent('not-found')

    const { wrapper } = await renderPage()

    expect(wrapper.find('.detail-state').text()).toBe(t('pages.eventDetail.notFound'))
    await vi.waitFor(() => expect(document.title).toContain(t('pages.eventDetail.seo.notFound')))
    expect(metaContent('meta[name="robots"]')).toBe('noindex')
  })

  it('switches between the information and activities tabs', async () => {
    serveEvent(buildEventResponse())
    serveSignupApi()

    const { wrapper } = await renderPage()
    const tabs = () => wrapper.findAll('.detail-tab')
    expect(tabs()[0]?.classes()).toContain('detail-tab--active')

    await wrapper.find('.detail-body__panel button').trigger('click')
    await flushPromises()
    expect(tabs()[1]?.classes()).toContain('detail-tab--active')
    const timeline = wrapper.findComponent(EventActivitiesTimeline)
    expect(timeline.props()).toMatchObject({
      eventId: 'event-1',
      signupOpen: true,
      earlyOnly: false,
      terms: null,
    })
    await vi.waitFor(() => expect(wrapper.find('.act__title').text()).toBe('Taller de robótica'))

    await tabs()[0]?.trigger('click')
    expect(wrapper.findComponent(EventActivitiesTimeline).exists()).toBe(false)

    await tabs()[1]?.trigger('click')
    expect(wrapper.findComponent(EventActivitiesTimeline).exists()).toBe(true)
  })

  describe('during early signup', () => {
    const earlyEvent = () =>
      buildEventResponse({
        earlySignupStartsAt: LONG_AGO,
        signupStartsAt: FAR_FUTURE,
        termsDocument: { id: 'terms-1', name: 'Normas', description: 'Sé amable' },
      })

    it('lists the early signup date and lets eligible users sign up', async () => {
      serveEvent(earlyEvent())
      serveSignupApi()

      const { wrapper } = await renderPage({ earlySignupEligible: true })

      expect(wrapper.findAll('.detail-panel__row')[1]?.text()).toContain(formatDateTime(LONG_AGO))
      expect(wrapper.text()).toContain(t('entities.event.status.earlySignupOpen'))
      await wrapper.findAll('.detail-tab')[1]?.trigger('click')

      expect(wrapper.findComponent(EventActivitiesTimeline).props()).toMatchObject({
        signupOpen: true,
        earlyOnly: false,
        terms: { id: 'terms-1', name: 'Normas', description: 'Sé amable' },
      })
    })

    it('keeps signup closed for users who are not eligible', async () => {
      serveEvent(earlyEvent())
      serveSignupApi()

      const { wrapper } = await renderPage({ earlySignupEligible: false })
      await wrapper.findAll('.detail-tab')[1]?.trigger('click')

      expect(wrapper.findComponent(EventActivitiesTimeline).props()).toMatchObject({
        signupOpen: false,
        earlyOnly: true,
      })
    })

    it('keeps signup closed for guests', async () => {
      serveEvent(earlyEvent())
      serveSignupApi()

      const { wrapper } = await renderPage()
      await wrapper.findAll('.detail-tab')[1]?.trigger('click')

      expect(wrapper.findComponent(EventActivitiesTimeline).props()).toMatchObject({
        signupOpen: false,
        earlyOnly: true,
      })
    })
  })

  it('does not open signup once the signup window has closed', async () => {
    serveEvent(buildEventResponse({ signupEndsAt: LONG_AGO }))
    serveSignupApi()

    const { wrapper } = await renderPage({ earlySignupEligible: true })
    await wrapper.findAll('.detail-tab')[1]?.trigger('click')

    expect(wrapper.findComponent(EventActivitiesTimeline).props()).toMatchObject({
      signupOpen: false,
      earlyOnly: false,
    })
  })
})
