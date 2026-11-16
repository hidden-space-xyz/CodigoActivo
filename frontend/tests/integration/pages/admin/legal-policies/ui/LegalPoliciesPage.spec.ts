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

    const { wrapper } = await renderApp('/admin/legal', { user: { staffRole: 'Administrator' } })

    const [privacy, cookies] = wrapper.findAll('.el-tab-pane')
    await vi.waitFor(() =>
      expect(cookies?.text()).toContain(t('pages.admin.legalPolicies.status.unpublished')),
    )
    expect(privacy?.text()).toContain(t('pages.admin.legalPolicies.privacyHint'))
    expect(cookies?.text()).not.toContain(t('pages.admin.legalPolicies.privacyHint'))
    expect(privacy?.findAll('.legal-policy-editor__version')).toHaveLength(2)
    expect(privacy?.isVisible()).toBe(true)

    await wrapper.findAll('.el-tabs__item')[1]?.trigger('click')

    expect(cookies?.isVisible()).toBe(true)
    expect(privacy?.isVisible()).toBe(false)
  })
})
