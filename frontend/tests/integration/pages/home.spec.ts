import type { VueWrapper } from '@vue/test-utils'
import type { JsonBodyType } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { FOUNDING_YEAR } from '@/shared/config'

import {
  buildAnnouncementItem,
  buildEventItem,
  buildPartner,
  useHomeApi,
} from '../../support/fixtures/auth-register/home'
import { renderApp, t } from '../../support/render'
import { http, HttpResponse, paged, server } from '../../support/server'

function activeSponsorName(wrapper: VueWrapper): string {
  return wrapper.get('.sponsor__name').text()
}

function sectionTitles(wrapper: VueWrapper): string[] {
  return wrapper.findAll('.home-section__title').map((title) => title.text())
}

function sponsorsSection(wrapper: VueWrapper) {
  return wrapper.get('.sponsors')
}

async function clickArrow(wrapper: VueWrapper, labelKey: string): Promise<void> {
  await sponsorsSection(wrapper)
    .get(`button[aria-label="${t(labelKey)}"]`)
    .trigger('click')
}

describe('home page', () => {
  it('shows the hero with calls to action and the founding year', async () => {
    useHomeApi()
    const { wrapper, router } = await renderApp('/')
    const hero = wrapper.get('.hero')

    expect(hero.get('h1').text()).toContain(t('pages.home.hero.titleLine1'))
    expect(hero.text()).toContain(t('pages.home.hero.badge', { year: FOUNDING_YEAR }))
    expect(hero.text()).toContain(t('pages.home.hero.stats.free'))
    expect(hero.findAll('.hero__actions a').map((link) => link.attributes('href'))).toEqual([
      router.resolve({ name: 'register' }).href,
      router.resolve({ name: 'about' }).href,
    ])
  })

  it('requests the featured-first announcements, events and sponsors', async () => {
    const { requests } = useHomeApi()
    await renderApp('/')

    await vi.waitFor(() => expect(requests).toHaveLength(4))
    const queries = requests.map((params) => Object.fromEntries(params.entries()))
    expect(queries).toEqual(
      expect.arrayContaining([
        { sort: '-featured,-createdAt', pageSize: '4' },
        { sort: '-featured,-createdAt', pageSize: '1' },
        { scope: 'Upcoming', sort: 'eventStartsAt', pageSize: '4' },
        { pageSize: '100', sort: 'tier,-fromDate' },
      ]),
    )
  })

  it('shows loading placeholders until announcements and events arrive', async () => {
    const releases: (() => void)[] = []
    const hold = (body: JsonBodyType) =>
      new Promise<Response>((resolve) => {
        releases.push(() => resolve(HttpResponse.json(body)))
      })
    server.use(
      http.get('/api/announcements', () => hold(paged([buildAnnouncementItem()]))),
      http.get('/api/events', () => hold(paged([buildEventItem()]))),
      http.get('/api/partners', () => HttpResponse.json(paged([]))),
    )

    const { wrapper } = await renderApp('/')

    expect(wrapper.findAll('.home-section__loading').map((p) => p.text())).toEqual([
      t('common.loading'),
      t('common.loading'),
    ])

    await vi.waitFor(() => expect(releases).toHaveLength(3))
    for (const release of releases) release()
    await vi.waitFor(() => expect(wrapper.find('.home-section__loading').exists()).toBe(false))
  })

  it('shows the featured announcement and the recent ones with a link to all', async () => {
    useHomeApi({
      announcements: [
        buildAnnouncementItem({ id: 'a1', title: 'Destacado', featured: true }),
        buildAnnouncementItem({ id: 'a2', title: 'Segundo' }),
        buildAnnouncementItem({ id: 'a3', title: 'Tercero' }),
      ],
    })
    const { wrapper, router } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.home.announcements.title')))
    const section = wrapper.findAll('.home-section')[0]
    expect(section?.text()).toContain('Destacado')
    expect(section?.findAll('.home-section__grid > *')).toHaveLength(2)
    expect(section?.get('.home-section__view-all').attributes('href')).toBe(
      router.resolve({ name: 'announcements' }).href,
    )
    expect(sectionTitles(wrapper)).toEqual([t('pages.home.announcements.title')])
  })

  it('shows the featured event without repeating it among the upcoming events', async () => {
    const featured = buildEventItem({ id: 'e1', title: 'Evento destacado', featured: true })
    useHomeApi({
      featuredEvents: [featured],
      upcomingEvents: [featured, buildEventItem({ id: 'e2', title: 'Otro evento' })],
    })
    const { wrapper, router } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.home.events.title')))
    const section = wrapper.get('.home-section')
    expect(section.text()).toContain('Evento destacado')
    expect(section.findAll('.home-section__grid > *')).toHaveLength(1)
    expect(section.get('.home-section__grid').text()).toContain('Otro evento')
    expect(section.get('.home-section__view-all').attributes('href')).toBe(
      router.resolve({ name: 'events' }).href,
    )
    expect(sectionTitles(wrapper)).toEqual([t('pages.home.events.title')])
  })

  it('hides the grids when there is only a featured item', async () => {
    useHomeApi({
      announcements: [buildAnnouncementItem({ featured: true })],
      featuredEvents: [buildEventItem()],
    })
    const { wrapper } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.findAll('.home-section')).toHaveLength(2))
    expect(wrapper.find('.home-section__grid').exists()).toBe(false)
  })

  it('renders sponsors as logos, initials and external links around the active one', async () => {
    useHomeApi({
      partners: [
        buildPartner({
          id: 'p1',
          name: 'Acme Labs',
          website: 'https://acme.test',
          thumbnailId: 'f1',
        }),
        buildPartner({ id: 'p2', name: '  Globex  ' }),
        buildPartner({ id: 'p3', name: 'Initech Software Group' }),
        buildPartner({ id: 'p4', name: '' }),
      ],
    })
    const { wrapper } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.findAll('.sponsor')).toHaveLength(3))
    const cards = wrapper.findAll('.sponsor')
    expect(sponsorsSection(wrapper).text()).toContain(t('pages.home.sponsors.heading'))
    expect(activeSponsorName(wrapper)).toBe('Acme Labs')

    expect(cards[0]?.element.tagName).toBe('A')
    expect(cards[0]?.attributes()).toMatchObject({
      href: 'https://acme.test',
      target: '_blank',
      rel: 'noopener',
    })
    expect(cards[0]?.get('img').attributes()).toMatchObject({
      src: '/api/files/f1/content',
      alt: 'Acme Labs',
    })

    expect(cards[1]?.element.tagName).toBe('DIV')
    expect(cards[1]?.attributes('href')).toBeUndefined()
    expect(cards[1]?.get('.sponsor__logo').classes()).toContain('sponsor__logo--color')
    expect(cards[1]?.get('.sponsor__logo').text()).toBe('GL')
    expect(cards[2]?.get('.sponsor__logo').text()).toBe('IS')
    expect(cards[2]?.get('.sponsor__logo').attributes('style')).toMatch(/--logo-color: var\(--ca-/)
  })

  it('hides far sponsors from assistive technology', async () => {
    useHomeApi({
      partners: Array.from({ length: 7 }, (_, i) =>
        buildPartner({ id: `p${i}`, name: `Partner ${i}` }),
      ),
    })
    const { wrapper } = await renderApp('/')

    await vi.waitFor(() => expect(wrapper.findAll('.sponsor')).toHaveLength(7))
    const hidden = wrapper
      .findAll('.sponsor')
      .map((card, index) => (card.attributes('aria-hidden') === 'true' ? index : null))
      .filter((index) => index !== null)
    expect(hidden).toEqual([3, 4])
    expect(wrapper.findAll('.sponsor')[3]?.classes()).toContain('sponsor--d3')
  })

  it('moves the sponsor carousel with the arrows and on a timer that pauses on interaction', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] })
    useHomeApi({
      partners: ['Alpha', 'Beta', 'Gamma'].map((name) => buildPartner({ id: name, name })),
    })
    const { wrapper } = await renderApp('/')
    await vi.waitFor(() => expect(wrapper.findAll('.sponsor')).toHaveLength(3))
    expect(activeSponsorName(wrapper)).toBe('Alpha')

    await clickArrow(wrapper, 'pages.home.sponsors.next')
    expect(activeSponsorName(wrapper)).toBe('Beta')
    await clickArrow(wrapper, 'pages.home.sponsors.prev')
    await clickArrow(wrapper, 'pages.home.sponsors.prev')
    expect(activeSponsorName(wrapper)).toBe('Gamma')

    vi.advanceTimersByTime(3000)
    await wrapper.vm.$nextTick()
    expect(activeSponsorName(wrapper)).toBe('Alpha')

    const firstCard = wrapper.findAll('.sponsor')[0]
    await firstCard?.trigger('mouseenter')
    vi.advanceTimersByTime(9000)
    await wrapper.vm.$nextTick()
    expect(activeSponsorName(wrapper)).toBe('Alpha')
    await firstCard?.trigger('mouseleave')

    await firstCard?.trigger('focusin')
    vi.advanceTimersByTime(9000)
    await wrapper.vm.$nextTick()
    expect(activeSponsorName(wrapper)).toBe('Alpha')
    await firstCard?.trigger('focusout')

    const carousel = wrapper.get('.sponsors__carousel')
    await carousel.trigger('touchstart')
    vi.advanceTimersByTime(9000)
    await wrapper.vm.$nextTick()
    expect(activeSponsorName(wrapper)).toBe('Alpha')
    await carousel.trigger('touchcancel')
    await carousel.trigger('touchstart')
    await carousel.trigger('touchend')

    vi.advanceTimersByTime(3000)
    await wrapper.vm.$nextTick()
    expect(activeSponsorName(wrapper)).toBe('Beta')
  })

  it('publishes the organization structured data', async () => {
    useHomeApi()
    await renderApp('/')

    const script = document.getElementById('ca-jsonld')
    expect(script?.getAttribute('type')).toBe('application/ld+json')
    expect(JSON.parse(script?.textContent ?? '{}')).toMatchObject({
      '@type': 'NGO',
      name: t('seo.siteName'),
    })
  })
})
