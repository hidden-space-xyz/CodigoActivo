import { describe, expect, it, vi } from 'vitest'

import {
  buildEventCategory,
  buildTermsDocument,
} from '../../../../support/fixtures/admin-content/builders'
import { renderApp, t } from '../../../../support/render'
import { http, HttpResponse, paged, server } from '../../../../support/server'

describe('admin catalogs page', () => {
  it('shows the event categories and terms documents catalogs', async () => {
    server.use(
      http.get('/api/events/categoryType', () => HttpResponse.json(paged([buildEventCategory()]))),
      http.get('/api/events/termsDocument', () => HttpResponse.json(paged([buildTermsDocument()]))),
    )

    const { wrapper } = await renderApp('/admin/catalogs', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Workshop'))
    expect(wrapper.text()).toContain(t('pages.admin.catalogs.title'))
    expect(wrapper.text()).toContain(t('features.manageCatalogs.title'))
    expect(wrapper.text()).toContain(t('features.manageCatalogs.terms.title'))
    expect(wrapper.text()).toContain('Privacy')
  })
})
