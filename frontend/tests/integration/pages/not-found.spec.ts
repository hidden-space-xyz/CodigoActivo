import { describe, expect, it, vi } from 'vitest'

import { useHomeApi } from '../../support/fixtures/auth-register/home'
import { renderApp, t } from '../../support/render'

describe('not found page', () => {
  it('renders for unknown paths with a noindex title', async () => {
    const { wrapper, router } = await renderApp('/this/page/does-not-exist')

    expect(router.currentRoute.value.name).toBe('not-found')
    expect(wrapper.get('h1').text()).toBe(t('pages.notFound.title'))
    expect(wrapper.text()).toContain(t('pages.notFound.text'))
    expect(document.title).toContain(t('seo.routes.notFound.title'))
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex',
    )
  })

  it('takes the visitor back home', async () => {
    useHomeApi()
    const { wrapper, router } = await renderApp('/missing')

    await wrapper.get('.not-found__actions a').trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.home.hero.subtitle')))
  })
})
