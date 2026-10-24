import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server, TEST_LEGAL_ENTITY } from '../../../../support/server'
import { buildLegalPolicy, richText } from '../../../../support/builders'

function publishedPrivacyPolicy(text = 'Tratamos tus datos para gestionar tu cuenta') {
  server.use(
    http.get('/api/legal-policies/Privacy', () =>
      HttpResponse.json(buildLegalPolicy({ content: richText(text) })),
    ),
  )
}

describe('privacy policy page', () => {
  it('identifies the controller before the policy in force', async () => {
    publishedPrivacyPolicy()

    const { wrapper } = await renderApp('/privacy')

    expect(wrapper.get('h1').text()).toBe(t('pages.privacyPolicy.title'))
    expect(document.title).toContain(t('seo.routes.privacyPolicy.title'))
    const controller = wrapper.get('[aria-labelledby="privacy-controller"]')
    await vi.waitFor(() =>
      expect(controller.findAll('.legal-entry__value').map((value) => value.text())).toEqual([
        TEST_LEGAL_ENTITY.name,
        TEST_LEGAL_ENTITY.taxId,
        TEST_LEGAL_ENTITY.address,
        TEST_LEGAL_ENTITY.registry,
        TEST_LEGAL_ENTITY.email,
        TEST_LEGAL_ENTITY.phone,
      ]),
    )
    await vi.waitFor(() =>
      expect(wrapper.get('.legal-policy').text()).toBe(
        'Tratamos tus datos para gestionar tu cuenta',
      ),
    )
    expect(
      controller.element.compareDocumentPosition(wrapper.get('.legal-policy').element) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })

  it('shows a dash for each legal detail the deployment left empty', async () => {
    publishedPrivacyPolicy()
    server.use(
      http.get('/api/legal-entity', () =>
        HttpResponse.json({
          name: '',
          taxId: '',
          address: '',
          registry: '',
          email: '',
          phone: null,
        }),
      ),
    )

    const { wrapper } = await renderApp('/privacy')

    const values = wrapper
      .get('[aria-labelledby="privacy-controller"]')
      .findAll('.legal-entry__value')
      .map((value) => value.text())
    expect(values).toEqual(['—', '—', '—', '—', '—', '—'])
  })

  it('tells visitors the policy is not published yet', async () => {
    server.use(http.get('/api/legal-policies/Privacy', () => apiError(404)))

    const { wrapper } = await renderApp('/privacy')

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('entities.legalPolicy.unpublished')))
    expect(wrapper.find('.legal-policy').exists()).toBe(false)
    expect(wrapper.find('.legal-document__updated').exists()).toBe(false)
  })

  it('opens from the footer link', async () => {
    publishedPrivacyPolicy()
    const { wrapper, router } = await renderApp('/about')

    const href = router.resolve({ name: 'privacy-policy' }).href
    await wrapper.get(`.footer__legal a[href="${href}"]`).trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('privacy-policy'), {
      timeout: 10_000,
    })
    await vi.waitFor(() => expect(wrapper.get('h1').text()).toBe(t('pages.privacyPolicy.title')), {
      timeout: 10_000,
    })
  }, 20_000)
})
