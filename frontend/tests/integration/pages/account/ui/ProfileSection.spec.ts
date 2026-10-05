import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDialog, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { currentUser } from '@/entities/session'
import { genderLabelKey } from '@/entities/user'
import ProfileSection from '@/pages/account/ui/ProfileSection.vue'
import type { UserResponse } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildUserResponse } from '../../../../support/builders'
import {
  click,
  findButton,
  findButtons,
  notificationTexts,
  openDialog,
  openDialogs,
  typeInto,
} from '../../../../support/dom'

function serveProfile(
  user: UserResponse = buildUserResponse({
    status: { id: 'status-active', name: 'Activo', color: '#00FF00' },
  }),
) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(user)))
}

async function renderSection(user: Parameters<typeof renderWithProviders>[1] = { user: {} }) {
  server.use(http.get('/api/users', () => HttpResponse.json({ items: [], total: 0 })))
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
  await click(findButtons(t('pages.account.profile.changePassword'), document.body)[0] as Element)
  return openDialog(t('pages.account.profile.changePassword'))
}

async function submitPassword(current: string, next: string, confirm: string) {
  const dialog = await openPasswordDialog()
  await typeInto('#p-cur', current, dialog)
  await typeInto('#p-new', next, dialog)
  await typeInto('#p-conf', confirm, dialog)
  await click(findButton(t('common.save'), dialog))
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

  it('shows the personal data of the signed-in user in the registration order', async () => {
    serveProfile(
      buildUserResponse({
        secondaryPhone: '622222222',
        status: { id: 'status-active', name: 'Activo', color: '#00FF00' },
      }),
    )

    await renderSection()

    expect(Object.entries(infoRows())).toEqual([
      [t('common.name'), 'Ada Lovelace'],
      [t('common.status'), 'Activo'],
      [t('common.nationalId'), '12345678Z'],
      [t('common.gender'), t(genderLabelKey('Female'))],
      [t('common.phone'), '600000000'],
      [t('common.secondaryPhone'), '622222222'],
      [t('common.email'), 'ada@example.test'],
      [t('common.promotionalConsent'), t('common.no')],
    ])
  })

  it('shows dashes for missing optional data', async () => {
    serveProfile(buildUserResponse({ email: null, phone: null }))

    await renderSection()

    expect(infoRows()).toMatchObject({
      [t('common.email')]: '—',
      [t('common.phone')]: '—',
      [t('common.secondaryPhone')]: '—',
    })
  })

  it('shows nothing when the session has expired', async () => {
    const { wrapper } = await renderSection()

    expect(wrapper.find('.acc-info').exists()).toBe(false)
    expect(wrapper.text()).not.toContain(t('common.loading'))
  })

  it('edits the profile with trimmed values, says a new email awaits its link and closes the dialog', async () => {
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

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    expect(dialog.querySelector<HTMLInputElement>('#p-firstname')?.value).toBe('Ada')
    expect(dialog.querySelector<HTMLInputElement>('#p-email')?.value).toBe('ada@example.test')
    expect(dialog.querySelector<HTMLInputElement>('#p-national-id')?.value).toBe('12345678Z')
    expect(dialog.querySelector('#p-national-id')?.getAttribute('autocapitalize')).toBe(
      'characters',
    )
    expect(dialog.querySelector('#p-national-id-confirm')).toBeNull()
    expect([...dialog.querySelectorAll('[id^="p-"]')].map((field) => field.id)).toEqual([
      'p-firstname',
      'p-lastname',
      'p-national-id',
      'p-gender',
      'p-phone',
      'p-secondary-phone',
      'p-email',
      'p-promotional-consent',
    ])
    expect(dialog.querySelector<HTMLInputElement>('#p-promotional-consent')?.checked).toBe(false)
    await typeInto('#p-firstname', '  Augusta ', dialog)
    await typeInto('#p-lastname', ' King ', dialog)
    await typeInto('#p-email', ' augusta@example.test ', dialog)
    await typeInto('#p-phone', ' 611111111 ', dialog)
    await typeInto('#p-secondary-phone', ' 622222222 ', dialog)
    await typeInto('#p-national-id', 'x-1234567-l', dialog)
    await click(dialog.querySelector('#p-promotional-consent') as Element)
    await selectGender(wrapper, 'Other')
    await typeInto('#p-current', 'old-password', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1',
      body: {
        firstName: 'Augusta',
        lastName: 'King',
        email: 'augusta@example.test',
        phone: '611111111',
        secondaryPhone: '622222222',
        birthDate: null,
        nationalId: 'X1234567L',
        promotionalConsent: true,
        gender: 'Other',
        parentId: null,
        currentPassword: 'old-password',
      },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.profile.emailChangeSentSummary'))
    expect(notificationTexts().join()).toContain(
      t('pages.account.profile.emailChangeSentDetail', { email: 'augusta@example.test' }),
    )
    expect(infoRows()[t('common.name')]).toBe('Augusta Lovelace')
    expect(infoRows()[t('common.email')]).toBe('ada@example.test')
    await vi.waitFor(() => expect(meRequests).toBe(2))
  })

  it('asks for the current password only when the email or the phone changes', async () => {
    serveProfile()
    const updated = vi.fn()
    server.use(
      http.put('/api/users/:userId', () => {
        updated()
        return apiError(400, 'UserCurrentPasswordIncorrect')
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    expect(dialog.querySelector('#p-current')).toBeNull()

    await typeInto('#p-email', 'augusta@example.test', dialog)
    expect(dialog.querySelector('#p-current')).not.toBeNull()
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('pages.account.profile.contactChange.passwordRequired'))
    expect(updated).not.toHaveBeenCalled()

    await typeInto('#p-current', 'wrong', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(updated).toHaveBeenCalledTimes(1))
    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(openDialogs()).toHaveLength(1)
  })

  it('asks for the current password when the secondary phone changes and sends a blank one as null', async () => {
    serveProfile(buildUserResponse({ secondaryPhone: '622222222' }))
    const bodies: unknown[] = []
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildUserResponse())
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    expect(dialog.querySelector<HTMLInputElement>('#p-secondary-phone')?.value).toBe('622222222')
    expect(dialog.querySelector('#p-current')).toBeNull()
    await typeInto('#p-secondary-phone', '   ', dialog)
    expect(dialog.querySelector('#p-current')).not.toBeNull()
    await typeInto('#p-current', 'old-password', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({ secondaryPhone: null, currentPassword: 'old-password' })
    await vi.waitFor(() =>
      expect(notificationTexts().join()).toContain(t('pages.account.profile.savedDetail')),
    )
    expect(notificationTexts().join()).toContain(t('pages.account.profile.savedSummary'))
  })

  it('refuses a secondary phone equal to the phone', async () => {
    serveProfile()
    const updated = vi.fn()
    server.use(
      http.put('/api/users/:userId', () => {
        updated()
        return HttpResponse.json(buildUserResponse())
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    await typeInto('#p-secondary-phone', ' 600000000 ', dialog)
    await typeInto('#p-current', 'old-password', dialog)
    expect(dialog.textContent).not.toContain(t('entities.user.person.sameAsPhone'))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('entities.user.person.sameAsPhone'))
    expect(dialog.querySelector('#p-secondary-phone')?.closest('.ca-invalid')).not.toBeNull()
    expect(updated).not.toHaveBeenCalled()
  })

  it('asks for the current password when the server refuses it without a visible change', async () => {
    serveProfile()
    const bodies: unknown[] = []
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        bodies.push(await request.json())
        return bodies.length === 1
          ? apiError(400, 'UserCurrentPasswordIncorrect')
          : HttpResponse.json(buildUserResponse())
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    expect(dialog.querySelector('#p-current')).toBeNull()
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(dialog.querySelector('#p-current')).not.toBeNull())
    expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect'))
    expect(notificationTexts()).toHaveLength(0)

    await typeInto('#p-current', 'old-password', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(bodies).toHaveLength(2))
    expect(bodies[0]).toMatchObject({ currentPassword: null })
    expect(bodies[1]).toMatchObject({ currentPassword: 'old-password' })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
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

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    await selectGender(wrapper, null)
    expect(dialog.textContent).not.toContain(t('entities.user.person.genderRequired'))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('entities.user.person.genderRequired'))
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(updated).not.toHaveBeenCalled()
  })

  it('asks no birth date and requires a DNI/NIE whose control letter matches', async () => {
    serveProfile()
    const bodies: unknown[] = []
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildUserResponse())
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))
    expect(dialog.querySelector('#p-dob')).toBeNull()

    await typeInto('#p-national-id', '12345678A', dialog)
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('validation.nationalIdLetter'))
    expect(bodies).toHaveLength(0)

    await typeInto('#p-national-id', 'X123456', dialog)
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('validation.nationalIdFormat'))
    expect(bodies).toHaveLength(0)

    await typeInto('#p-national-id', ' x 1234567 l ', dialog)
    expect(dialog.textContent).not.toContain(t('validation.nationalIdFormat'))
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({ nationalId: 'X1234567L' })
  })

  it('shows the promotional consent the user gave', async () => {
    serveProfile(buildUserResponse({ promotionalConsent: true }))

    await renderSection()

    expect(infoRows()[t('common.promotionalConsent')]).toBe(t('common.yes'))
  })

  it('opens an empty edit form when the profile is unavailable', async () => {
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    const dialog = openDialog(t('pages.account.profile.editDialogHeader'))

    for (const id of [
      'p-firstname',
      'p-lastname',
      'p-email',
      'p-phone',
      'p-secondary-phone',
      'p-national-id',
    ]) {
      expect(dialog.querySelector<HTMLInputElement>(`#${id}`)?.value).toBe('')
    }
    await click(findButton(t('common.cancel'), dialog))
    expect(openDialogs()).toHaveLength(0)
  })

  it('notifies and keeps the dialog open when the profile cannot be saved', async () => {
    serveProfile()
    server.use(http.put('/api/users/:userId', () => apiError(404, 'UserNotFound')))
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    await click(
      findButton(t('common.save'), openDialog(t('pages.account.profile.editDialogHeader'))),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.UserNotFound'))
    expect(openDialogs()).toHaveLength(1)
  })

  it('explains that disposable email addresses are refused for security', async () => {
    serveProfile()
    server.use(http.put('/api/users/:userId', () => apiError(400, 'DisposableEmailNotAllowed')))
    await renderSection()

    await click(findButton(t('pages.account.profile.editData'), document.body))
    await click(
      findButton(t('common.save'), openDialog(t('pages.account.profile.editDialogHeader'))),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.DisposableEmailNotAllowed'))
    expect(t('errors.DisposableEmailNotAllowed')).toMatch(/seguridad/)
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

  it('rejects a new password equal to the current one', async () => {
    serveProfile()
    await renderSection()

    const dialog = await submitPassword(
      'a-long-password-1',
      'a-long-password-1',
      'a-long-password-1',
    )

    expect(dialog.querySelector('.acc-form__error')?.textContent.trim()).toBe(
      t('validation.newPasswordSameAsCurrent'),
    )
  })

  it('rejects a confirmation that does not match the new password', async () => {
    serveProfile()
    await renderSection()

    const dialog = await submitPassword('current', 'a-long-password-1', 'a-long-password-2')

    expect(dialog.querySelector('.acc-form__error')?.textContent.trim()).toBe(
      t('validation.passwordsMismatch'),
    )
  })

  it('changes the password, then ends the session and asks to sign in again', async () => {
    serveProfile()
    let received: { path: string; body: unknown } | undefined
    const logout = vi.fn()
    server.use(
      http.patch('/api/users/:userId/password', async ({ request }) => {
        received = { path: new URL(request.url).pathname, body: await request.json() }
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('/api/auth/logout', () => {
        logout()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router, queryClient } = await renderSection()

    await submitPassword('old-password', 'a-long-password-1', 'a-long-password-1')

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1/password',
      body: { currentPassword: 'old-password', newPassword: 'a-long-password-1' },
    })
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(openDialogs()).toHaveLength(0)
    expect(currentUser(queryClient)).toBeNull()
    expect(logout).toHaveBeenCalledOnce()
    expect(notificationTexts().join()).toContain(t('pages.account.profile.passwordUpdatedSummary'))
    expect(notificationTexts().join()).toContain(t('pages.account.profile.passwordUpdatedDetail'))
  })

  it('resets the password form on reopening', async () => {
    serveProfile()
    await renderSection()

    const dialog = await submitPassword('current', 'short', 'short')
    await click(findButton(t('common.cancel'), dialog))

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
        t('errors.UserCurrentPasswordIncorrect'),
      ),
    )
    expect(openDialogs()).toHaveLength(1)
    await click(findButton(t('common.cancel'), dialog))
    expect(openDialogs()).toHaveLength(0)
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

    await click(findButton(t('pages.account.profile.editData'), document.body))
    await dismiss(t('pages.account.profile.editDialogHeader'))
    expect(openDialogs()).toHaveLength(0)

    await openPasswordDialog()
    await dismiss(t('pages.account.profile.changePassword'))
    expect(openDialogs()).toHaveLength(0)
  })
})
