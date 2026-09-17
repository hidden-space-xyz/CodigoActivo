import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { renderCertificatePreview } from '@/features/account/model/certificate-sheet'

import {
  buildCertificateResponse,
  buildChildResponse,
  buildHistoryResponse,
} from '../../../support/fixtures/account/account'
import { buildUserResponse } from '../../../support/fixtures/user'
import { renderApp, t } from '../../../support/render'
import { http, HttpResponse, server } from '../../../support/server'

vi.mock('@/features/account/model/certificate-sheet', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/account/model/certificate-sheet')>()),
  renderCertificatePreview: vi.fn(),
}))

function serveAccount() {
  server.use(
    http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())),
    http.get('/api/users', () => HttpResponse.json({ items: [buildChildResponse()], total: 1 })),
    http.get('/api/me/event-history', () =>
      HttpResponse.json([buildHistoryResponse({ title: 'Campus de verano' })]),
    ),
    http.get('/api/me/certificates', () =>
      HttpResponse.json([buildCertificateResponse({ code: 'CA-2025-0042' })]),
    ),
  )
}

function tabLabels(): string[] {
  return [...document.querySelectorAll('.el-tabs__item')].map((tab) => tab.textContent.trim())
}

function activePanelText(): string {
  const panel = [...document.querySelectorAll<HTMLElement>('.el-tab-pane')].find(
    (candidate) => candidate.style.display !== 'none',
  )
  return panel?.textContent ?? ''
}

async function selectTab(label: string): Promise<void> {
  const tab = [...document.querySelectorAll<HTMLElement>('.el-tabs__item')].find(
    (candidate) => candidate.textContent.trim() === label,
  )
  if (!tab) throw new Error(`No tab "${label}"`)
  tab.click()
  await flushPromises()
}

beforeEach(() => {
  vi.mocked(renderCertificatePreview).mockReset().mockResolvedValue()
})

describe('account page', () => {
  it('sends guests to the login page', async () => {
    const { router } = await renderApp('/account')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
  })

  it('shows the profile and minors on the default tab', async () => {
    serveAccount()

    const { wrapper } = await renderApp('/account', { user: {} })
    await vi.waitFor(() => expect(wrapper.text()).toContain('Byron Lovelace'))

    expect(wrapper.text()).toContain(t('pages.account.title'))
    expect(wrapper.text()).toContain(t('pages.account.intro'))
    expect(tabLabels()).toEqual([
      t('pages.account.tabs.profile'),
      t('pages.account.tabs.history'),
      t('pages.account.tabs.certificates'),
    ])
    expect(activePanelText()).toContain(t('features.account.profile.lead'))
    expect(activePanelText()).toContain(t('features.account.minors.title'))
    expect(wrapper.text()).not.toContain('Campus de verano')
  })

  it('switches tabs and keeps the selection in the address', async () => {
    serveAccount()
    const { wrapper, router } = await renderApp('/account', { user: {} })

    await selectTab(t('pages.account.tabs.history'))
    await vi.waitFor(() => expect(router.currentRoute.value.query).toEqual({ tab: 'history' }))
    await vi.waitFor(() => expect(activePanelText()).toContain('Campus de verano'))

    await selectTab(t('pages.account.tabs.certificates'))
    await vi.waitFor(() => expect(router.currentRoute.value.query).toEqual({ tab: 'certificates' }))
    await vi.waitFor(() => expect(activePanelText()).toContain('CA-2025-0042'))

    await selectTab(t('pages.account.tabs.profile'))
    await vi.waitFor(() => expect(router.currentRoute.value.query).toEqual({}))
    expect(router.currentRoute.value.name).toBe('account')
    expect(wrapper.text()).toContain(t('features.account.profile.lead'))
  })

  it('opens the tab named in the address', async () => {
    serveAccount()

    await renderApp('/account?tab=certificates', { user: {} })

    await vi.waitFor(() => expect(activePanelText()).toContain('CA-2025-0042'))
    expect(activePanelText()).toContain(t('features.account.certificates.lead'))
  })

  it('falls back to the profile tab for an unknown tab in the address', async () => {
    serveAccount()

    await renderApp('/account?tab=billing', { user: {} })

    await vi.waitFor(() => expect(activePanelText()).toContain(t('features.account.profile.lead')))
  })
})
