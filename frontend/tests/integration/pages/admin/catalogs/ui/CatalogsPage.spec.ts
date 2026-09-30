import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../../support/render'
import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildEventCategoryType, buildTermsDocument } from '../../../../../support/builders'

describe('admin catalogs page', () => {
  it('shows the event categories and terms documents catalogs', async () => {
    server.use(
      http.get('/api/events/categoryType', () =>
        HttpResponse.json(paged([buildEventCategoryType()])),
      ),
      http.get('/api/events/termsDocument', () => HttpResponse.json(paged([buildTermsDocument()]))),
    )

    const { wrapper } = await renderApp('/admin/catalogs', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Workshop'))
    expect(wrapper.text()).toContain(t('pages.admin.catalogs.header.title'))
    expect(wrapper.text()).toContain(t('pages.admin.catalogs.eventCategories.title'))
    expect(wrapper.text()).toContain(t('pages.admin.catalogs.termsDocuments.title'))
    expect(wrapper.text()).toContain('Privacy')
  })
})
