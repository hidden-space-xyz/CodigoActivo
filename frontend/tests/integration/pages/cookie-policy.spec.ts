import { describe, expect, it, vi } from 'vitest'

import { registerStaleBuildReload } from '@/app/config'
import { CONTACT } from '@/shared/config'
import { useTheme } from '@/shared/lib'

import { renderApp, t } from '../../support/render'

function triggerStaleBuildReload(): void {
  const events = new EventTarget()
  registerStaleBuildReload({
    addEventListener: events.addEventListener.bind(events),
    sessionStorage,
    navigator: { onLine: true },
    location: { reload: vi.fn() },
  } as unknown as Window)
  events.dispatchEvent(new Event('vite:preloadError', { cancelable: true }))
}

describe('cookie policy page', () => {
  it('lists every cookie and browser storage entry with its purpose and duration', async () => {
    const { wrapper } = await renderApp('/cookies')

    expect(wrapper.get('h1').text()).toBe(t('pages.cookiePolicy.title'))
    const entries = wrapper.findAll('.cookie-policy__entry')
    expect(entries.map((entry) => entry.get('.cookie-policy__name').text())).toEqual([
      '__Host-CodigoActivo.Session',
      '__Host-CodigoActivo.TwoFactor',
      '__Host-CodigoActivo.Csrf',
      'ca-theme',
      'ca:stale-build-reload',
    ])
    for (const entry of entries) {
      const values = entry.findAll('.cookie-policy__fact-value').map((value) => value.text())
      expect(values).toHaveLength(2)
      expect(values.every((value) => value.length > 0)).toBe(true)
    }
    expect(entries[0]?.text()).toContain(t('pages.cookiePolicy.used.session.duration'))
    expect(document.title).toContain(t('seo.routes.cookiePolicy.title'))
  })

  it('names every key the application writes to browser storage', async () => {
    useTheme().setTheme('light')
    triggerStaleBuildReload()
    const written = [...Object.keys(localStorage), ...Object.keys(sessionStorage)]

    const { wrapper } = await renderApp('/cookies')

    const listed = wrapper.findAll('.cookie-policy__name').map((name) => name.text())
    expect(written).toEqual(['ca-theme', 'ca:stale-build-reload'])
    expect(listed).toEqual(expect.arrayContaining(written))
  })

  it('links to the contact address, the authority, its cookie guide and browser help', async () => {
    const { wrapper } = await renderApp('/cookies')

    const main = wrapper.get('main')
    const hrefs = main.findAll('a').map((link) => link.attributes('href'))
    expect(hrefs).toEqual(
      expect.arrayContaining([
        `mailto:${CONTACT.email}`,
        'https://www.aepd.es',
        'https://www.aepd.es/guias/guia-cookies.pdf',
        'https://support.google.com/chrome/answer/95647?hl=es',
      ]),
    )
    expect(main.findAll('.cookie-policy__guides a')).toHaveLength(5)
    for (const link of main.findAll('a[target="_blank"]')) {
      expect(link.attributes('rel')).toBe('noopener')
    }
    expect(main.text()).toContain(t('pages.cookiePolicy.consent.keepSignedIn'))
  })

  it('opens from the footer link', async () => {
    const { wrapper, router } = await renderApp('/about')

    await wrapper.get('.footer__legal a').trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('cookie-policy'), {
      timeout: 10_000,
    })
    await vi.waitFor(() => expect(wrapper.get('h1').text()).toBe(t('pages.cookiePolicy.title')), {
      timeout: 10_000,
    })
  }, 20_000)
})
