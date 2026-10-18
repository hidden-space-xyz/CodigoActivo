import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import PersonalDataSection from '@/pages/account/ui/PersonalDataSection.vue'

import { renderWithProviders, t } from '../../../../support/render'
import { http, HttpResponse, server } from '../../../../support/server'
import { click, findButton } from '../../../../support/dom'

describe('PersonalDataSection', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('explains the copy and downloads it from its button', async () => {
    let requested = false
    server.use(
      http.get('/api/me/personal-data', () => {
        requested = true
        return HttpResponse.json({ account: { firstName: 'Ada' } })
      }),
    )
    const clicked = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { wrapper } = await renderWithProviders(PersonalDataSection, {
      attach: true,
      route: '/account',
      user: {},
    })
    await flushPromises()

    expect(wrapper.text()).toContain(t('pages.account.personalData.title'))
    expect(wrapper.text()).toContain(t('pages.account.personalData.lead'))
    await click(findButton(t('pages.account.personalData.download'), document.body))

    await vi.waitFor(() => expect(clicked).toHaveBeenCalledOnce())
    expect(requested).toBe(true)
  })
})
