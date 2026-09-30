import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import DeleteAccountSection from '@/pages/account/ui/DeleteAccountSection.vue'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import {
  click,
  findButton,
  findButtons,
  notificationTexts,
  openDialog,
  openDialogs,
  typeInto,
} from '../../../../support/dom'
import { sessionOf } from '../../../../support/session'

const ACTION = 'pages.account.deleteAccount.action'
const HEADER = 'pages.account.deleteAccount.dialogHeader'
const SEND_CODE = 'pages.account.deleteAccount.sendCode'
const CONTINUE = 'pages.account.deleteAccount.continue'
const SUBMIT = 'pages.account.deleteAccount.submit'

async function renderSection(options: Parameters<typeof renderWithProviders>[1] = { user: {} }) {
  const rendered = await renderWithProviders(DeleteAccountSection, {
    attach: true,
    route: '/account',
    ...options,
  })
  await flushPromises()
  return rendered
}

function serveDeletion() {
  const calls: { url: string; body: unknown }[] = []
  server.use(
    http.post('/api/me/deletion/code', async ({ request }) => {
      calls.push({ url: '/api/me/deletion/code', body: await request.json() })
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/me/deletion', async ({ request }) => {
      calls.push({ url: '/api/me/deletion', body: await request.json() })
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/auth/logout', () => {
      calls.push({ url: '/api/auth/logout', body: null })
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return calls
}

async function openDeletionDialog(): Promise<HTMLElement> {
  await click(findButton(t(ACTION), document.body))
  return openDialog(t(HEADER))
}

async function acceptConsent(dialog: HTMLElement): Promise<void> {
  const checkbox = dialog.querySelector<HTMLInputElement>('#del-accept')
  if (!checkbox) throw new Error('No consent checkbox')
  await click(checkbox)
}

describe('DeleteAccountSection', () => {
  it('warns about what is deleted and offers the action to members', async () => {
    await renderSection()

    expect(document.body.textContent).toContain(t('pages.account.deleteAccount.title'))
    expect(document.body.textContent).toContain(t('pages.account.deleteAccount.lead'))
    expect(findButtons(t(ACTION), document.body)).toHaveLength(1)
    expect(document.body.textContent).not.toContain(
      t('pages.account.deleteAccount.initialAdminNote'),
    )
  })

  it('offers the action to an administrator the API allows', async () => {
    server.use(http.get('/api/me/deletion', () => HttpResponse.json({ allowed: true })))
    await renderSection({ user: { isAdmin: true } })

    await vi.waitFor(() => expect(findButtons(t(ACTION), document.body)).toHaveLength(1))
    expect(document.body.textContent).not.toContain(
      t('pages.account.deleteAccount.initialAdminNote'),
    )
  })

  it('hides the action from the initial administrator and explains why', async () => {
    server.use(http.get('/api/me/deletion', () => HttpResponse.json({ allowed: false })))
    await renderSection({ user: { isAdmin: true } })

    await vi.waitFor(() =>
      expect(document.body.textContent).toContain(
        t('pages.account.deleteAccount.initialAdminNote'),
      ),
    )
    expect(findButtons(t(ACTION), document.body)).toHaveLength(0)
  })

  it('emails a code and then deletes the account, signs out and goes home', async () => {
    const calls = serveDeletion()
    const { router, queryClient } = await renderSection()
    queryClient.setQueryData(['cached', 'elsewhere'], 'stale')

    const dialog = await openDeletionDialog()
    await click(findButton(t(SEND_CODE), dialog))
    expect(dialog.textContent).toContain(
      t('pages.account.deleteAccount.form.problems.passwordRequired'),
    )
    expect(calls).toHaveLength(0)

    await typeInto('#del-password', 'secret', dialog)
    await click(findButton(t(SEND_CODE), dialog))

    await vi.waitFor(() => expect(dialog.querySelector('#del-code')).not.toBeNull())
    expect(dialog.textContent).toContain(
      t('pages.account.deleteAccount.codeSent', { email: 'ada@example.test' }),
    )
    expect(findButton(t(SUBMIT), dialog).disabled).toBe(true)

    await typeInto('#del-code', ' 123456 ', dialog)
    expect(findButton(t(SUBMIT), dialog).disabled).toBe(true)
    await acceptConsent(dialog)
    await click(findButton(t(SUBMIT), dialog))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
    expect(calls).toEqual([
      { url: '/api/me/deletion/code', body: { currentPassword: 'secret' } },
      { url: '/api/me/deletion', body: { currentPassword: 'secret', code: '123456' } },
      { url: '/api/auth/logout', body: null },
    ])
    expect(sessionOf(queryClient).isAuthenticated).toBe(false)
    expect(queryClient.getQueryData(['cached', 'elsewhere'])).toBeUndefined()
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.deleteAccount.deletedSummary'))
  })

  it('skips the email for an authenticator and asks for the application code', async () => {
    const calls = serveDeletion()
    const { router } = await renderSection({ user: { twoFactorMethod: 'Authenticator' } })

    const dialog = await openDeletionDialog()
    await typeInto('#del-password', 'secret', dialog)
    await click(findButton(t(CONTINUE), dialog))

    expect(dialog.textContent).toContain(t('pages.account.deleteAccount.authenticatorIntro'))
    expect(findButtons(t('pages.account.deleteAccount.resend'), dialog)).toHaveLength(0)
    expect(calls).toHaveLength(0)

    await typeInto('#del-code', '654321', dialog)
    await acceptConsent(dialog)
    await click(findButton(t(SUBMIT), dialog))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
    expect(calls).toEqual([
      { url: '/api/me/deletion', body: { currentPassword: 'secret', code: '654321' } },
      { url: '/api/auth/logout', body: null },
    ])
  })

  it('keeps the password step open when the password is not accepted', async () => {
    server.use(
      http.post('/api/me/deletion/code', () => apiError(400, 'UserCurrentPasswordIncorrect')),
    )
    const { queryClient } = await renderSection()

    const dialog = await openDeletionDialog()
    await typeInto('#del-password', 'wrong', dialog)
    await click(findButton(t(SEND_CODE), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.UserCurrentPasswordIncorrect'),
      ),
    )
    expect(dialog.querySelector('#del-code')).toBeNull()
    expect(sessionOf(queryClient).isAuthenticated).toBe(true)
  })

  it('shows a rejected code and lets the user ask for another one', async () => {
    const codeRequests: unknown[] = []
    server.use(
      http.post('/api/me/deletion/code', async ({ request }) => {
        codeRequests.push(await request.json())
        return codeRequests.length > 1
          ? apiError(409, 'TwoFactorResendCooldownActive')
          : new HttpResponse(null, { status: 204 })
      }),
      http.post('/api/me/deletion', () => apiError(400, 'TwoFactorCodeInvalid')),
    )
    const { router } = await renderSection()

    const dialog = await openDeletionDialog()
    await typeInto('#del-password', 'secret', dialog)
    await click(findButton(t(SEND_CODE), dialog))
    await vi.waitFor(() => expect(dialog.querySelector('#del-code')).not.toBeNull())

    await typeInto('#del-code', '000000', dialog)
    await acceptConsent(dialog)
    await click(findButton(t(SUBMIT), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorCodeInvalid'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)
    expect(router.currentRoute.value.name).toBe('account')

    await click(findButton(t('pages.account.deleteAccount.resend'), dialog))

    await vi.waitFor(() =>
      expect(dialog.querySelector('[role="alert"]')?.textContent).toBe(
        t('errors.TwoFactorResendCooldownActive'),
      ),
    )
    expect(codeRequests).toEqual([{ currentPassword: 'secret' }, { currentPassword: 'secret' }])
  })

  it('refuses to submit the form until the consent box is ticked', async () => {
    const calls = serveDeletion()
    await renderSection()

    const dialog = await openDeletionDialog()
    await typeInto('#del-password', 'secret', dialog)
    await click(findButton(t(SEND_CODE), dialog))
    await vi.waitFor(() => expect(dialog.querySelector('#del-code')).not.toBeNull())
    await typeInto('#del-code', '123456', dialog)

    const form = dialog.querySelector('form')
    if (!form) throw new Error('No deletion form')
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    await flushPromises()

    expect(calls).toEqual([{ url: '/api/me/deletion/code', body: { currentPassword: 'secret' } }])
    expect(openDialogs()).toHaveLength(1)
  })

  it('forgets the typed password, code and error when the dialog is cancelled', async () => {
    server.use(
      http.post('/api/me/deletion/code', () => apiError(400, 'UserCurrentPasswordIncorrect')),
    )
    const { queryClient } = await renderSection()

    const dialog = await openDeletionDialog()
    await typeInto('#del-password', 'wrong', dialog)
    await click(findButton(t(SEND_CODE), dialog))
    await vi.waitFor(() => expect(dialog.querySelector('[role="alert"]')).not.toBeNull())
    await click(findButton(t('common.cancel'), dialog))
    expect(openDialogs()).toHaveLength(0)

    const reopened = await openDeletionDialog()
    expect(reopened.querySelector('[role="alert"]')).toBeNull()
    expect(reopened.querySelector<HTMLInputElement>('#del-password')?.value).toBe('')
    expect(reopened.querySelector('#del-code')).toBeNull()
    expect(sessionOf(queryClient).isAuthenticated).toBe(true)
  })
})
