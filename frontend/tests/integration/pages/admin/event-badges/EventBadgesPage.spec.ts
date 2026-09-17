import { flushPromises } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { EventBadgesPage } from '@/pages/admin/event-badges'
import type { EventBadgeResponse, EventBadgesResponse } from '@/shared/api/generated/models'

import {
  buildBadge,
  EVENT_ID,
  stubDocumentFonts,
} from '../../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

let restoreFonts: () => void = () => undefined

beforeEach(() => {
  restoreFonts = stubDocumentFonts()
})

afterEach(() => {
  restoreFonts()
})

async function renderBadges(badges: EventBadgeResponse[], title: string | null = 'Hackathon') {
  server.use(
    http.get('/api/reports/events/:eventId/badges', () =>
      HttpResponse.json({ eventId: EVENT_ID, title, badges } satisfies EventBadgesResponse),
    ),
  )
  const rendered = await renderWithProviders(EventBadgesPage, {
    route: `/admin/events/${EVENT_ID}/badges`,
    attach: true,
  })
  await vi.waitFor(() => expect(rendered.wrapper.find('.badge').exists()).toBe(badges.length > 0))
  await flushPromises()
  return rendered
}

function styleVar(element: Element | undefined, name: string): number {
  return Number((element as HTMLElement | undefined)?.style.getPropertyValue(name))
}

describe('EventBadgesPage', () => {
  it('renders one badge per attendee with brand, event, type, guardian and activities', async () => {
    const { wrapper } = await renderBadges([
      buildBadge({
        guardian: { firstName: 'Mary', lastName: 'Lovelace', phone: '600111222' },
        activities: ['A1', 'A2', 'A3', 'A4', 'A5', 'A6', 'A7', 'A8'],
      }),
      buildBadge({
        userId: 'user-2',
        firstName: 'Tim',
        lastName: null,
        userTypeName: null,
        userTypeColor: '#fafafa',
        guardian: { firstName: null, phone: null },
        activities: [],
      }),
      buildBadge({ userId: 'user-3', firstName: 'Grace', userTypeColor: 'not-a-color' }),
    ])

    const [ada, tim, grace] = wrapper.findAll('.badge')
    expect(wrapper.find('.back').attributes('href')).toBe(`/admin/events/${EVENT_ID}`)
    expect(ada?.find('.badge__brand').text()).toContain(t('seo.siteName'))
    expect(ada?.find('.badge__event').text()).toBe('Hackathon')
    expect(ada?.find('.badge__name').text()).toBe('Ada Lovelace')
    expect(ada?.attributes('style')).toContain('--accent: #123456')
    expect(ada?.find('.badge__type').text()).toBe('Member')
    expect(ada?.find('.badge__guardian-name').text()).toBe('Mary')
    expect(ada?.find('.badge__guardian-phone').text()).toContain('600111222')
    expect(ada?.findAll('.badge__activity').map((chip) => chip.text())).toEqual([
      'A1',
      'A2',
      'A3',
      'A4',
      'A5',
      'A6',
      t('pages.admin.eventBadges.moreActivities', { n: 2 }),
    ])

    expect(tim?.find('.badge__name').text()).toBe('Tim')
    expect(tim?.attributes('style')).toContain('--accent: #475569')
    expect(tim?.find('.badge__type').text()).toBe('—')
    expect(tim?.find('.badge__guardian-name').text()).toBe('—')
    expect(tim?.find('.badge__guardian-phone').exists()).toBe(false)
    expect(tim?.find('.badge__activities').exists()).toBe(false)

    expect(grace?.attributes('style')).toContain('--accent: #475569')
    expect(grace?.find('.badge__guardian').exists()).toBe(false)
    expect(grace?.findAll('.badge__activity')).toHaveLength(1)
  })

  it('splits badges into A4 sheets of sixteen', async () => {
    const badges = Array.from({ length: 17 }, (_, index) =>
      buildBadge({ userId: `user-${index}`, firstName: `Person ${index}` }),
    )

    const { wrapper } = await renderBadges(badges, null)

    const sheets = wrapper.findAll('.sheet')
    expect(sheets).toHaveLength(2)
    expect(sheets[0]?.findAll('.badge')).toHaveLength(16)
    expect(sheets[1]?.findAll('.badge')).toHaveLength(1)
    expect(sheets[0]?.find('.badge__event').text()).toBe('')
  })

  it('adds the A4 print rule while mounted and prints on demand', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined)
    const { wrapper } = await renderBadges([buildBadge()])

    const pageRule = () =>
      [...document.head.querySelectorAll('style')].some((style) =>
        style.textContent?.includes('@page { size: A4 portrait; margin: 0; }'),
      )
    expect(pageRule()).toBe(true)

    await wrapper.find('.print-btn').trigger('click')
    expect(print).toHaveBeenCalledTimes(1)

    wrapper.unmount()
    expect(pageRule()).toBe(false)
  })

  it('shows the empty and error states', async () => {
    const empty = await renderBadges([])
    await vi.waitFor(() =>
      expect(empty.wrapper.text()).toContain(t('pages.admin.eventBadges.emptyText')),
    )
    empty.wrapper.unmount()

    server.use(http.get('/api/reports/events/:eventId/badges', () => apiError(500)))
    const failing = await renderWithProviders(EventBadgesPage, {
      route: `/admin/events/${EVENT_ID}/badges`,
    })
    await vi.waitFor(() => expect(failing.wrapper.text()).toContain(t('dataState.error')))
  })

  it('shrinks long names and dense content until the badge fits', async () => {
    const fitOf = (element: Element) => {
      const badge = element.closest('.badge')
      return {
        fit: styleVar(badge ?? undefined, '--fit') || 1,
        nameFit: styleVar(badge ?? undefined, '--name-fit') || 1,
        long: /Maximiliana|Bartolomeo/.test(
          badge?.querySelector('.badge__name')?.textContent ?? '',
        ),
        dense: badge?.querySelector('.badge__name')?.textContent?.includes('Maximiliana') ?? false,
      }
    }
    vi.spyOn(Element.prototype, 'scrollWidth', 'get').mockImplementation(function (this: Element) {
      if (!this.classList.contains('badge__name')) return 0
      const { nameFit, long } = fitOf(this)
      return long ? 500 * nameFit + 20 : 0
    })
    vi.spyOn(Element.prototype, 'clientWidth', 'get').mockImplementation(() => 300)
    vi.spyOn(Element.prototype, 'clientHeight', 'get').mockImplementation(() => 150)
    vi.spyOn(Element.prototype, 'scrollHeight', 'get').mockImplementation(function (this: Element) {
      if (!this.classList.contains('badge__body')) return 0
      const { fit, nameFit, long, dense } = fitOf(this)
      if (!long) return 0
      return 100 * fit + (dense ? 200 : 50) * nameFit
    })
    const originalStyle = window.getComputedStyle.bind(window)
    vi.spyOn(window, 'getComputedStyle').mockImplementation((element, pseudo) => {
      if (element.classList.contains('badge__body')) {
        return { paddingLeft: '10px', paddingRight: '10px' } as CSSStyleDeclaration
      }
      return originalStyle(element, pseudo)
    })

    const { wrapper } = await renderBadges([
      buildBadge({ firstName: 'Maximiliana', lastName: 'de Todos los Santos y Montenegro' }),
      buildBadge({ userId: 'user-2', firstName: 'Bo', activities: null }),
      buildBadge({ userId: 'user-3', firstName: 'Bartolomeo', lastName: 'Fernández de Córdoba' }),
    ])

    const [long, short, medium] = wrapper.findAll('.badge')
    await vi.waitFor(() => expect(styleVar(long?.element, '--name-fit')).toBeGreaterThan(0))
    const nameFit = styleVar(long?.element, '--name-fit')
    const fit = styleVar(long?.element, '--fit')
    expect(nameFit).toBeGreaterThanOrEqual(0.45)
    expect(nameFit).toBeLessThan(0.5)
    expect(fit).toBeGreaterThanOrEqual(0.6)
    expect(fit).toBeLessThan(1.5)
    expect(100 * fit + 200 * nameFit).toBeLessThanOrEqual(151)
    const mediumFit = styleVar(medium?.element, '--fit')
    expect(mediumFit).toBeGreaterThan(0.6)
    expect(mediumFit).toBeLessThan(1.5)
    expect(styleVar(medium?.element, '--name-fit')).toBeGreaterThan(0.5)
    expect(short?.find('.badge__activities').exists()).toBe(false)
    expect(styleVar(short?.element, '--fit')).toBe(1.5)
    expect(short?.attributes('style')).not.toContain('--name-fit')
  })
})
