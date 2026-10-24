import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildLegalPolicy, richText } from '../../../../support/builders'

describe('cookie policy page', () => {
  it('shows the cookie policy in force with its version and date', async () => {
    server.use(
      http.get('/api/legal-policies/Cookies', () =>
        HttpResponse.json(
          buildLegalPolicy({
            kind: 'Cookies',
            version: 4,
            content: richText('Solo cookies propias'),
          }),
        ),
      ),
    )

    const { wrapper } = await renderApp('/cookies')

    expect(wrapper.get('h1').text()).toBe(t('pages.cookiePolicy.title'))
    expect(document.title).toContain(t('seo.routes.cookiePolicy.title'))
    await vi.waitFor(() => expect(wrapper.get('.legal-policy').text()).toBe('Solo cookies propias'))
    expect(wrapper.get('.legal-document__updated').text()).toContain('4')
  })

  it('opens from the footer link', async () => {
    server.use(http.get('/api/legal-policies/Cookies', () => apiError(404)))
    const { wrapper, router } = await renderApp('/about')

    const href = router.resolve({ name: 'cookie-policy' }).href
    await wrapper.get(`.footer__legal a[href="${href}"]`).trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('cookie-policy'), {
      timeout: 10_000,
    })
    await vi.waitFor(() => expect(wrapper.get('h1').text()).toBe(t('pages.cookiePolicy.title')), {
      timeout: 10_000,
    })
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('entities.legalPolicy.unpublished')))
  }, 20_000)
})
