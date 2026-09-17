import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDialog, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { genderLabel } from '@/entities/user'
import ProfileSection from '@/features/account/ui/ProfileSection.vue'
import type { UserResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import {
  buttonByText,
  buttonsByText,
  click,
  dialogByTitle,
  fill,
  notificationTexts,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { omit } from '../../../../support/fixtures/account/account'
import { buildAuthUser, buildUserResponse } from '../../../../support/fixtures/user'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

function serveProfile(user: UserResponse = buildUserResponse({ status: { name: 'Activo' } })) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(user)))
}

async function renderSection(user: Parameters<typeof renderWithProviders>[1] = { user: {} }) {
  const rendered = await renderWithProviders(ProfileSection, { attach: true, ...user })
  await flushPromises()
  return rendered
}

function infoRows(): Record<string, string> {
  return Object.fromEntries(
    [...document.querySelectorAll('.acc-info__row')].map((row) => [
      row.querySelector('dt')?.textContent.trim() ?? '',
      row.querySelector('dd')?.textContent.trim() ?? '',
    ]),
  )
}

async function openPasswordDialog(): Promise<HTMLElement> {
  await click(
    buttonsByText(document.body, t('features.account.profile.changePassword'))[0] as Element,
  )
  return dialogByTitle(t('features.account.profile.changePassword'))
}

async function submitPassword(current: string, next: string, confirm: string) {
  const dialog = await openPasswordDialog()
  await fill(dialog, '#p-cur', current)
  await fill(dialog, '#p-new', next)
  await fill(dialog, '#p-conf', confirm)
  await click(buttonByText(dialog, t('common.save')))
  return dialog
}

async function selectGender(wrapper: VueWrapper, value: string | null): Promise<void> {
  wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', value)
  await flushPromises()
}

describe('ProfileSection', () => {
  it('shows a loading state while the profile is requested', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('/api/auth/me', async () => {
        await gate
        return HttpResponse.json(buildUserResponse())
      }),
    )

    const { wrapper } = await renderSection()
    expect(wrapper.text()).toContain(t('common.loading'))

    release()
    await vi.waitFor(() => expect(wrapper.find('.acc-info').exists()).toBe(true))
  })

  it('shows the personal data of the signed-in user', async () => {
    serveProfile()

    await renderSection()

    expect(infoRows()).toEqual({
      [t('common.name')]: 'Ada Lovelace',
      [t('common.email')]: 'ada@example.test',
      [t('common.phone')]: '600000000',
      [t('common.birthDate')]: formatDate('1990-05-10'),
      [t('common.gender')]: genderLabel('Female'),
      [t('common.status')]: 'Activo',
    })
  })

  it('shows dashes for missing optional data', async () => {
    serveProfile(
      omit(buildUserResponse({ email: null, phone: null, status: { name: null } }), 'gender'),
    )

    await renderSection()

    expect(infoRows()).toMatchObject({
      [t('common.email')]: '—',
      [t('common.phone')]: '—',
      [t('common.gender')]: '—',
      [t('common.status')]: '—',
    })
  })

  it('shows nothing when the session has expired', async () => {
    const { wrapper } = await renderSection()

    expect(wrapper.find('.acc-info').exists()).toBe(false)
    expect(wrapper.text()).not.toContain(t('common.loading'))
  })

  it('offers account deletion to members but not to administrators', async () => {
    serveProfile()
    await renderSection()
    expect(buttonsByText(document.body, t('features.account.profile.deleteAccount'))).toHaveLength(
      1,
    )

    useSession().setUser(buildAuthUser({ isAdmin: true }))
    await flushPromises()

    expect(buttonsByText(document.body, t('features.account.profile.deleteAccount'))).toHaveLength(
      0,
    )
  })

  it('edits the profile with trimmed values, confirms and closes the dialog', async () => {
    let meRequests = 0
    server.use(
      http.get('/api/auth/me', () => {
        meRequests += 1
        return HttpResponse.json(buildUserResponse())
      }),
    )
    let received: { path: string; body: unknown } | undefined
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        received = { path: new URL(request.url).pathname, body: await request.json() }
        return HttpResponse.json(buildUserResponse({ firstName: 'Augusta', phone: '611111111' }))
      }),
    )
    const { wrapper } = await renderSection()

    await click(buttonByText(document.body, t('features.account.profile.editData')))
    const dialog = dialogByTitle(t('features.account.profile.editDialogHeader'))
    expect(dialog.querySelector<HTMLInputElement>('#p-firstname')?.value).toBe('Ada')
    expect(dialog.querySelector<HTMLInputElement>('#p-email')?.value).toBe('ada@example.test')
    expect(dialog.querySelector<HTMLInputElement>('#p-dob')?.value).toBe('1990-05-10')
    await fill(dialog, '#p-firstname', '  Augusta ')
    await fill(dialog, '#p-lastname', ' King ')
    await fill(dialog, '#p-email', ' augusta@example.test ')
    await fill(dialog, '#p-phone', ' 611111111 ')
    await fill(dialog, '#p-dob', '1990-06-11')
    await selectGender(wrapper, 'Other')
    await click(buttonByText(dialog, t('common.save')))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1',
      body: {
        firstName: 'Augusta',
        lastName: 'King',
        email: 'augusta@example.test',
        phone: '611111111',
        birthDate: '1990-06-11',
        gender: 'Other',
        parentId: null,
      },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('features.account.profile.savedSummary'))
    expect(notificationTexts().join()).toContain(t('features.account.profile.savedDetail'))
    expect(infoRows()[t('common.name')]).toBe('Augusta Lovelace')
    await vi.waitFor(() => expect(meRequests).toBe(2))
  })

  it('requires a gender before saving the profile', async () => {
    serveProfile()
    const updated = vi.fn()
    server.use(
      http.put('/api/users/:userId', () => {
        updated()
        return HttpResponse.json(buildUserResponse())
      }),
    )
    const { wrapper } = await renderSection()

    await click(buttonByText(document.body, t('features.account.profile.editData')))
    const dialog = dialogByTitle(t('features.account.profile.editDialogHeader'))
    await selectGender(wrapper, null)
    expect(dialog.textContent).not.toContain(t('validation.genderRequired'))
    await click(buttonByText(dialog, t('common.save')))

    expect(dialog.textContent).toContain(t('validation.genderRequired'))
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(updated).not.toHaveBeenCalled()
  })

  it('opens an empty edit form when the profile is unavailable', async () => {
    await renderSection()

    await click(buttonByText(document.body, t('features.account.profile.editData')))
    const dialog = dialogByTitle(t('features.account.profile.editDialogHeader'))

    for (const id of ['p-firstname', 'p-lastname', 'p-email', 'p-phone', 'p-dob']) {
      expect(dialog.querySelector<HTMLInputElement>(`#${id}`)?.value).toBe('')
    }
    await click(buttonByText(dialog, t('common.cancel')))
    expect(openDialogs()).toHaveLength(0)
  })

  it('notifies and keeps the dialog open when the profile cannot be saved', async () => {
    serveProfile()
    server.use(http.put('/api/users/:userId', () => apiError(404, 'UserNotFound')))
    await renderSection()

    await click(buttonByText(document.body, t('features.account.profile.editData')))
    await click(
      buttonByText(dialogByTitle(t('features.account.profile.editDialogHeader')), t('common.save')),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.UserNotFound'))
    expect(openDialogs()).toHaveLength(1)
  })

  it('rejects new passwords shorter than twelve characters', async () => {
    serveProfile()
    const patched = vi.fn()
    server.use(
      http.patch('/api/users/:userId/password', () => {
        patched()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    const dialog = await submitPassword('current', 'short', 'short')

    expect(dialog.querySelector('.acc-form__error')?.textContent.trim()).toBe(
      t('validation.newPasswordMin'),
    )
    expect(patched).not.toHaveBeenCalled()
  })

  it('rejects a confirmation that does not match the new password', async () => {
    serveProfile()
    await renderSection()

    const dialog = await submitPassword('current', 'a-long-password-1', 'a-long-password-2')

    expect(dialog.querySelector('.acc-form__error')?.textContent.trim()).toBe(
      t('validation.passwordsMismatch'),
    )
  })

  it('changes the password, confirms it and resets the form on reopening', async () => {
    serveProfile()
    let received: { path: string; body: unknown } | undefined
    server.use(
      http.patch('/api/users/:userId/password', async ({ request }) => {
        received = { path: new URL(request.url).pathname, body: await request.json() }
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await submitPassword('old-password', 'a-long-password-1', 'a-long-password-1')

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1/password',
      body: { currentPassword: 'old-password', newPassword: 'a-long-password-1' },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(
      t('features.account.profile.passwordUpdatedSummary'),
    )

    const reopened = await openPasswordDialog()
    expect(reopened.querySelector<HTMLInputElement>('#p-cur')?.value).toBe('')
    expect(reopened.querySelector('.acc-form__error')).toBeNull()
  })

  it('explains when the current password is not accepted', async () => {
    serveProfile()
    server.use(
      http.patch('/api/users/:userId/password', () =>
        apiError(400, 'UserCurrentPasswordIncorrect'),
      ),
    )
    await renderSection()

    const dialog = await submitPassword('wrong', 'a-long-password-1', 'a-long-password-1')

    await vi.waitFor(() =>
      expect(dialog.querySelector('.acc-form__error')?.textContent.trim()).toBe(
        t('features.account.profile.passwordChangeFailed'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)
    await click(buttonByText(dialog, t('common.cancel')))
    expect(openDialogs()).toHaveLength(0)
  })

  it('deletes the account after confirmation, signs out and goes home', async () => {
    serveProfile()
    const calls: string[] = []
    server.use(
      http.delete('/api/users/:userId', ({ request }) => {
        calls.push(`DELETE ${new URL(request.url).pathname}`)
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('/api/auth/logout', () => {
        calls.push('logout')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router } = await renderSection({ user: {}, route: '/account' })

    await click(buttonByText(document.body, t('features.account.profile.deleteAccount')))
    const dialog = dialogByTitle(t('features.account.profile.deleteAccount'))
    expect(dialog.textContent).toContain(t('features.account.profile.deleteConfirm'))
    await click(buttonByText(dialog, t('common.delete')))

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))
    expect(calls).toEqual(['DELETE /api/users/user-1', 'logout'])
    expect(useSession().isAuthenticated).toBe(false)
  })

  it('keeps the account when deletion is cancelled', async () => {
    serveProfile()
    await renderSection()

    await click(buttonByText(document.body, t('features.account.profile.deleteAccount')))
    await click(
      buttonByText(dialogByTitle(t('features.account.profile.deleteAccount')), t('common.cancel')),
    )

    expect(openDialogs()).toHaveLength(0)
    expect(useSession().isAuthenticated).toBe(true)
  })

  it('closes each dialog when it is dismissed', async () => {
    serveProfile()
    const { wrapper } = await renderSection()
    const dismiss = async (title: string) => {
      const dialog = wrapper
        .findAllComponents(ElDialog)
        .find((candidate) => candidate.props('title') === title)
      if (!dialog) throw new Error(`No dialog titled "${title}"`)
      dialog.vm.$emit('update:modelValue', false)
      await flushPromises()
    }

    await click(buttonByText(document.body, t('features.account.profile.editData')))
    await dismiss(t('features.account.profile.editDialogHeader'))
    expect(openDialogs()).toHaveLength(0)

    await openPasswordDialog()
    await dismiss(t('features.account.profile.changePassword'))
    expect(openDialogs()).toHaveLength(0)

    await click(buttonByText(document.body, t('features.account.profile.deleteAccount')))
    await dismiss(t('features.account.profile.deleteAccount'))
    expect(openDialogs()).toHaveLength(0)
  })

  it('notifies when the account cannot be deleted', async () => {
    serveProfile()
    server.use(http.delete('/api/users/:userId', () => apiError(500)))
    const { router } = await renderSection({ user: {}, route: '/account' })

    await click(buttonByText(document.body, t('features.account.profile.deleteAccount')))
    await click(
      buttonByText(dialogByTitle(t('features.account.profile.deleteAccount')), t('common.delete')),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.generic'))
    expect(notificationTexts()[0]).toContain('trace-123')
    expect(router.currentRoute.value.name).toBe('account')
    expect(useSession().isAuthenticated).toBe(true)
  })
})
