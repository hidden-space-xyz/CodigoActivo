import { describe, expect, it, vi } from 'vitest'

import { PROCESSING_ACTIVITIES } from '@/pages/privacy-policy/config/processing'
import { CONTACT } from '@/shared/config'

import { renderApp, t } from '../../../../support/render'
import { http, HttpResponse, server, TEST_LEGAL_ENTITY } from '../../../../support/server'

describe('privacy policy page', () => {
  it('identifies the controller and its contact details', async () => {
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
        CONTACT.email,
        CONTACT.phone,
      ]),
    )
    expect(controller.get('p a').attributes('href')).toBe(`mailto:${CONTACT.email}`)
  })

  it('shows a dash for each legal detail the deployment left empty', async () => {
    server.use(
      http.get('/api/legal-entity', () =>
        HttpResponse.json({ name: '', taxId: '', address: '', registry: '' }),
      ),
    )

    const { wrapper } = await renderApp('/privacy')

    const values = wrapper
      .get('[aria-labelledby="privacy-controller"]')
      .findAll('.legal-entry__value')
      .map((value) => value.text())
    expect(values.slice(0, 4)).toEqual(['—', '—', '—', '—'])
  })

  it('states the data, purpose, legal basis and retention of every processing activity', async () => {
    const { wrapper } = await renderApp('/privacy')

    const activities = wrapper.get('[aria-labelledby="privacy-processing"]').findAll('.legal-entry')
    expect(activities.map((activity) => activity.get('h3').text())).toEqual(
      PROCESSING_ACTIVITIES.map((activity) => t(activity.titleKey)),
    )
    for (const activity of activities) {
      expect(activity.findAll('.legal-entry__label').map((label) => label.text())).toEqual([
        t('pages.privacyPolicy.processing.labels.data'),
        t('pages.privacyPolicy.processing.labels.purpose'),
        t('pages.privacyPolicy.processing.labels.legalBasis'),
        t('pages.privacyPolicy.processing.labels.retention'),
      ])
      const values = activity.findAll('.legal-entry__value').map((value) => value.text())
      expect(values.every((value) => value.length > 0)).toBe(true)
    }
  })

  it('explains the rights, the right to object apart, and where to exercise them', async () => {
    const { wrapper, router } = await renderApp('/privacy')

    const rights = wrapper.get('[aria-labelledby="privacy-rights"]')
    expect(rights.get('strong').text()).toBe(t('pages.privacyPolicy.rights.objection'))
    const hrefs = rights.findAll('a').map((link) => link.attributes('href'))
    expect(hrefs).toEqual([
      router.resolve({ name: 'account' }).href,
      `mailto:${CONTACT.email}`,
      'https://www.aepd.es',
    ])
    for (const link of wrapper.get('main').findAll('a[target="_blank"]')) {
      expect(link.attributes('rel')).toBe('noopener')
    }
    expect(wrapper.get('[aria-labelledby="privacy-cookies"] a').attributes('href')).toBe(
      router.resolve({ name: 'cookie-policy' }).href,
    )
  })

  it('opens from the footer link', async () => {
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
