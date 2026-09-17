import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import TwoFactorSection from '@/features/account/ui/TwoFactorSection.vue'

import {
  buttonByText,
  click,
  dialogByTitle,
  fill,
  notificationTexts,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { buildUserResponse } from '../../../../support/fixtures/user'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

const SETUP_TITLE = 'features.account.twoFactor.setupHeader'
const DISABLE_TITLE = 'features.account.twoFactor.disableHeader'
const SETUP = {
  sharedKey: 'JBSW Y3DP EHPK 3PXP',
  authenticatorUri: 'otpauth://totp/x?secret=JBSWY3DPEHPK3PXP',
}

async function renderSection(user: Parameters<typeof renderWithProviders>[1] = { user: {} }) {
  const rendered = await renderWithProviders(TwoFactorSection, { attach: true, ...user })
  await flushPromises()
  return rendered
}

function methodText(): string {
  return document.querySelector('[data-testid="two-factor-method"]')?.textContent.trim() ?? ''
}

function serveSetup(response: () => Response = () => HttpResponse.json(SETUP)) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/two-factor/authenticator/setup', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

function serveConfirm(response: () => Response = () => new HttpResponse(null, { status: 204 })) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/two-factor/authenticator/confirm', async ({ request }) => {
      bodies.push(await request.json())
      return response()
    }),
  )
  return bodies
}

describe('TwoFactorSection', () => {
  it('shows the email method and offers the authenticator', async () => {
    await renderSection()

    expect(methodText()).toBe(t('features.account.twoFactor.methods.Email'))
    expect(document.body.textContent).toContain(t('features.account.twoFactor.securityEmail'))
    expect(
      buttonByText(document.body, t('features.account.twoFactor.useAuthenticator')),
    ).toBeTruthy()
  })

  it('shows the authenticator method and offers going back to email', async () => {
    await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    expect(methodText()).toBe(t('features.account.twoFactor.methods.Authenticator'))
    expect(document.body.textContent).toContain(
      t('features.account.twoFactor.securityAuthenticator'),
    )
    expect(buttonByText(document.body, t('features.account.twoFactor.useEmail'))).toBeTruthy()
  })

  it('enrolls an authenticator in two steps and updates the shown method', async () => {
    const setups = serveSetup()
    const confirms = serveConfirm()
    server.use(
      http.get('/api/auth/me', () =>
        HttpResponse.json(buildUserResponse({ twoFactorMethod: 'Authenticator' })),
      ),
    )
    await renderSection()

    await click(buttonByText(document.body, t('features.account.twoFactor.useAuthenticator')))
    const dialog = dialogByTitle(t(SETUP_TITLE))
    await click(buttonByText(dialog, t('features.account.twoFactor.continue')))
    expect(dialog.textContent).toContain(t('features.account.twoFactor.passwordRequired'))
    expect(setups).toHaveLength(0)

    await fill(dialog, '#tf-setup-password', 'secret')
    await click(buttonByText(dialog, t('features.account.twoFactor.continue')))
    await vi.waitFor(() => expect(dialog.querySelector('#tf-setup-code')).not.toBeNull())
    expect(setups).toEqual([{ currentPassword: 'secret' }])
    expect(dialog.querySelector('[data-testid="two-factor-shared-key"]')?.textContent).toBe(
      SETUP.sharedKey,
    )
    await vi.waitFor(() =>
      expect(dialog.querySelector('img')?.getAttribute('src')).toContain('data:image/svg+xml'),
    )

    await click(buttonByText(dialog, t('features.account.twoFactor.confirm')))
    expect(dialog.textContent).toContain(t('features.account.twoFactor.codeRequired'))
    expect(confirms).toHaveLength(0)

    await fill(dialog, '#tf-setup-code', ' 123456 ')
    await click(buttonByText(dialog, t('features.account.twoFactor.confirm')))

    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(confirms).toEqual([{ code: '123456' }])
    await vi.waitFor(() =>
      expect(methodText()).toBe(t('features.account.twoFactor.methods.Authenticator')),
    )
    expect(notificationTexts().join()).toContain(t('features.account.twoFactor.enabledSummary'))
  })

  it('shows the API errors inside the enrollment dialog and resets it when cancelled', async () => {
    serveSetup(() => apiError(400, 'UserCurrentPasswordIncorrect'))
    await renderSection()

    await click(buttonByText(document.body, t('features.account.twoFactor.useAuthenticator')))
    const dialog = dialogByTitle(t(SETUP_TITLE))
    await fill(dialog, '#tf-setup-password', 'wrong')
    await click(buttonByText(dialog, t('features.account.twoFactor.continue')))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.UserCurrentPasswordIncorrect'),
      ),
    )
    expect(dialog.querySelector('#tf-setup-code')).toBeNull()

    await click(buttonByText(dialog, t('common.cancel')))
    expect(openDialogs()).toHaveLength(0)

    await click(buttonByText(document.body, t('features.account.twoFactor.useAuthenticator')))
    const reopened = dialogByTitle(t(SETUP_TITLE))
    expect(reopened.querySelector('[role="alert"]')).toBeNull()
    expect(reopened.querySelector<HTMLInputElement>('#tf-setup-password')?.value).toBe('')
  })

  it('keeps the confirmation step open when the code is rejected', async () => {
    serveSetup()
    serveConfirm(() => apiError(400, 'TwoFactorCodeInvalid'))
    await renderSection()

    await click(buttonByText(document.body, t('features.account.twoFactor.useAuthenticator')))
    const dialog = dialogByTitle(t(SETUP_TITLE))
    await fill(dialog, '#tf-setup-password', 'secret')
    await click(buttonByText(dialog, t('features.account.twoFactor.continue')))
    await vi.waitFor(() => expect(dialog.querySelector('#tf-setup-code')).not.toBeNull())
    await fill(dialog, '#tf-setup-code', '000000')
    await click(buttonByText(dialog, t('features.account.twoFactor.confirm')))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorCodeInvalid'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)
    expect(methodText()).toBe(t('features.account.twoFactor.methods.Email'))
  })

  it('returns to email after the password and the code are accepted', async () => {
    let body: unknown
    server.use(
      http.post('/api/auth/two-factor/email', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())),
    )
    await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    await click(buttonByText(document.body, t('features.account.twoFactor.useEmail')))
    const dialog = dialogByTitle(t(DISABLE_TITLE))
    await click(buttonByText(dialog, t('features.account.twoFactor.useEmail')))
    expect(dialog.textContent).toContain(t('features.account.twoFactor.passwordRequired'))
    expect(dialog.textContent).toContain(t('features.account.twoFactor.codeRequired'))
    expect(body).toBeUndefined()

    await fill(dialog, '#tf-disable-password', 'secret')
    await fill(dialog, '#tf-disable-code', '654321')
    await click(buttonByText(dialog, t('features.account.twoFactor.useEmail')))

    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(body).toEqual({ currentPassword: 'secret', code: '654321' })
    await vi.waitFor(() => expect(methodText()).toBe(t('features.account.twoFactor.methods.Email')))
    expect(useSession().user?.twoFactorMethod).toBe('Email')
    expect(notificationTexts().join()).toContain(t('features.account.twoFactor.disabledSummary'))
  })

  it('shows the error and keeps the dialog when the authenticator cannot be removed', async () => {
    server.use(http.post('/api/auth/two-factor/email', () => apiError(400, 'TwoFactorCodeInvalid')))
    await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    await click(buttonByText(document.body, t('features.account.twoFactor.useEmail')))
    const dialog = dialogByTitle(t(DISABLE_TITLE))
    await fill(dialog, '#tf-disable-password', 'secret')
    await fill(dialog, '#tf-disable-code', '000000')
    await click(buttonByText(dialog, t('features.account.twoFactor.useEmail')))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorCodeInvalid'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)

    await click(buttonByText(dialog, t('common.cancel')))
    expect(openDialogs()).toHaveLength(0)
  })
})
