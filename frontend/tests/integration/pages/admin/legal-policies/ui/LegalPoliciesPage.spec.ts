import { describe, expect, it, vi } from 'vitest'

import { renderApp, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import { buildLegalPolicy, buildLegalPolicyVersion } from '../../../../../support/builders'

describe('admin legal policies page', () => {
  it('edits the privacy and the cookie policy in separate tabs', async () => {
    server.use(
      http.get('/api/legal-policies/Privacy', () => HttpResponse.json(buildLegalPolicy())),
      http.get('/api/legal-policies/Privacy/versions', () =>
        HttpResponse.json([buildLegalPolicyVersion({ version: 2 }), buildLegalPolicyVersion()]),
      ),
      http.get('/api/legal-policies/Cookies', () => apiError(404)),
      http.get('/api/legal-policies/Cookies/versions', () => apiError(404)),
    )

    const { wrapper } = await renderApp('/admin/legal', { user: { isAdmin: true } })

    expect(wrapper.get('h1').text()).toBe(t('pages.admin.legalPolicies.header.title'))
    expect(wrapper.findAll('.el-tabs__item').map((tab) => tab.text())).toEqual([
      t('pages.admin.legalPolicies.tabs.Privacy'),
      t('pages.admin.legalPolicies.tabs.Cookies'),
    ])
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.legalPolicies.status.unpublished')),
    )
    expect(wrapper.text()).toContain(t('pages.admin.legalPolicies.privacyHint'))
    expect(wrapper.findAll('.legal-policy-editor__version')).toHaveLength(2)
  })
})
