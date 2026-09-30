import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import TwoFactorSection from '@/pages/account/ui/TwoFactorSection.vue'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildUserResponse } from '../../../../support/builders'
import {
  click,
  findButton,
  notificationTexts,
  openDialog,
  openDialogs,
  typeInto,
} from '../../../../support/dom'
import { sessionOf } from '../../../../support/session'

const SETUP_TITLE = 'pages.account.twoFactor.setupHeader'
const DISABLE_TITLE = 'pages.account.twoFactor.disableHeader'
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

    expect(methodText()).toBe(t('pages.account.twoFactor.methods.Email'))
    expect(document.body.textContent).toContain(t('pages.account.twoFactor.securityEmail'))
    expect(findButton(t('pages.account.twoFactor.useAuthenticator'), document.body)).toBeTruthy()
  })

  it('shows the authenticator method and offers going back to email', async () => {
    await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    expect(methodText()).toBe(t('pages.account.twoFactor.methods.Authenticator'))
    expect(document.body.textContent).toContain(t('pages.account.twoFactor.securityAuthenticator'))
    expect(findButton(t('pages.account.twoFactor.useEmail'), document.body)).toBeTruthy()
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

    await click(findButton(t('pages.account.twoFactor.useAuthenticator'), document.body))
    const dialog = openDialog(t(SETUP_TITLE))
    await click(findButton(t('pages.account.twoFactor.continue'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.account.twoFactor.form.problems.passwordRequired'),
    )
    expect(setups).toHaveLength(0)

    await typeInto('#tf-setup-password', 'secret', dialog)
    await click(findButton(t('pages.account.twoFactor.continue'), dialog))
    await vi.waitFor(() => expect(dialog.querySelector('#tf-setup-code')).not.toBeNull())
    expect(setups).toEqual([{ currentPassword: 'secret' }])
    expect(dialog.querySelector('[data-testid="two-factor-shared-key"]')?.textContent).toBe(
      SETUP.sharedKey,
    )
    await vi.waitFor(() =>
      expect(dialog.querySelector('img')?.getAttribute('src')).toContain('data:image/svg+xml'),
    )

    await click(findButton(t('pages.account.twoFactor.confirm'), dialog))
    expect(dialog.textContent).toContain(t('pages.account.twoFactor.form.problems.codeRequired'))
    expect(confirms).toHaveLength(0)

    await typeInto('#tf-setup-code', ' 123456 ', dialog)
    await click(findButton(t('pages.account.twoFactor.confirm'), dialog))

    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(confirms).toEqual([{ code: '123456' }])
    await vi.waitFor(() =>
      expect(methodText()).toBe(t('pages.account.twoFactor.methods.Authenticator')),
    )
    expect(notificationTexts().join()).toContain(t('pages.account.twoFactor.enabledSummary'))
  })

  it('shows the API errors inside the enrollment dialog and resets it when cancelled', async () => {
    serveSetup(() => apiError(400, 'UserCurrentPasswordIncorrect'))
    await renderSection()

    await click(findButton(t('pages.account.twoFactor.useAuthenticator'), document.body))
    const dialog = openDialog(t(SETUP_TITLE))
    await typeInto('#tf-setup-password', 'wrong', dialog)
    await click(findButton(t('pages.account.twoFactor.continue'), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.UserCurrentPasswordIncorrect'),
      ),
    )
    expect(dialog.querySelector('#tf-setup-code')).toBeNull()

    await click(findButton(t('common.cancel'), dialog))
    expect(openDialogs()).toHaveLength(0)

    await click(findButton(t('pages.account.twoFactor.useAuthenticator'), document.body))
    const reopened = openDialog(t(SETUP_TITLE))
    expect(reopened.querySelector('[role="alert"]')).toBeNull()
    expect(reopened.querySelector<HTMLInputElement>('#tf-setup-password')?.value).toBe('')
  })

  it('keeps the confirmation step open when the code is rejected', async () => {
    serveSetup()
    serveConfirm(() => apiError(400, 'TwoFactorCodeInvalid'))
    await renderSection()

    await click(findButton(t('pages.account.twoFactor.useAuthenticator'), document.body))
    const dialog = openDialog(t(SETUP_TITLE))
    await typeInto('#tf-setup-password', 'secret', dialog)
    await click(findButton(t('pages.account.twoFactor.continue'), dialog))
    await vi.waitFor(() => expect(dialog.querySelector('#tf-setup-code')).not.toBeNull())
    await typeInto('#tf-setup-code', '000000', dialog)
    await click(findButton(t('pages.account.twoFactor.confirm'), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorCodeInvalid'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)
    expect(methodText()).toBe(t('pages.account.twoFactor.methods.Email'))
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
    const { queryClient } = await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    await click(findButton(t('pages.account.twoFactor.useEmail'), document.body))
    const dialog = openDialog(t(DISABLE_TITLE))
    await click(findButton(t('pages.account.twoFactor.useEmail'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.account.twoFactor.form.problems.passwordRequired'),
    )
    expect(dialog.textContent).toContain(t('pages.account.twoFactor.form.problems.codeRequired'))
    expect(body).toBeUndefined()

    await typeInto('#tf-disable-password', 'secret', dialog)
    await typeInto('#tf-disable-code', '654321', dialog)
    await click(findButton(t('pages.account.twoFactor.useEmail'), dialog))

    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(body).toEqual({ currentPassword: 'secret', code: '654321' })
    await vi.waitFor(() => expect(methodText()).toBe(t('pages.account.twoFactor.methods.Email')))
    expect(sessionOf(queryClient).user?.twoFactorMethod).toBe('Email')
    expect(notificationTexts().join()).toContain(t('pages.account.twoFactor.disabledSummary'))
  })

  it('shows the error and keeps the dialog when the authenticator cannot be removed', async () => {
    server.use(http.post('/api/auth/two-factor/email', () => apiError(400, 'TwoFactorCodeInvalid')))
    await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    await click(findButton(t('pages.account.twoFactor.useEmail'), document.body))
    const dialog = openDialog(t(DISABLE_TITLE))
    await typeInto('#tf-disable-password', 'secret', dialog)
    await typeInto('#tf-disable-code', '000000', dialog)
    await click(findButton(t('pages.account.twoFactor.useEmail'), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorCodeInvalid'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)

    await click(findButton(t('common.cancel'), dialog))
    expect(openDialogs()).toHaveLength(0)
  })
})
