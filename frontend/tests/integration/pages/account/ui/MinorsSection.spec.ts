import { flushPromises } from '@vue/test-utils'
import { ElDialog, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import MinorsSection from '@/pages/account/ui/MinorsSection.vue'
import { genderLabelKey, minorBirthDateRange } from '@/entities/user'
import type { UserResponse } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildDependentResponse, buildUserResponse } from '../../../../support/builders'
import {
  click,
  findButton,
  notificationTexts,
  openDialog,
  openDialogs,
  typeInto,
} from '../../../../support/dom'
import { formatDate } from '@/shared/lib/date'

const BYRON = buildDependentResponse()
const ANNE = buildDependentResponse({
  id: 'child-2',
  firstName: 'Anne',
  lastName: 'King',
  birthDate: '2018-11-20',
  gender: 'Female',
})

function serveChildren(children: UserResponse[] = [BYRON, ANNE]) {
  let requests = 0
  server.use(
    http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())),
    http.get('/api/users', () => {
      requests += 1
      return HttpResponse.json({ items: children, total: children.length })
    }),
  )
  return { count: () => requests }
}

async function renderSection() {
  const rendered = await renderWithProviders(MinorsSection, { user: {}, attach: true })
  await flushPromises()
  return rendered
}

function minorItem(name: string): HTMLElement {
  const item = [...document.querySelectorAll<HTMLElement>('.acc-minor')].find(
    (candidate) => candidate.querySelector('.acc-minor__name')?.textContent.trim() === name,
  )
  if (!item) throw new Error(`No minor "${name}"`)
  return item
}

function selectGender(
  wrapper: Awaited<ReturnType<typeof renderSection>>['wrapper'],
  value: string,
) {
  wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', value)
  return flushPromises()
}

async function fillMinor(dialog: HTMLElement): Promise<void> {
  await typeInto('#m-firstname', 'Grace', dialog)
  await typeInto('#m-lastname', 'Hopper', dialog)
  await typeInto('#m-dob', '2016-12-09', dialog)
}

describe('MinorsSection', () => {
  it('shows a loading state and then explains there are no minors', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())),
      http.get('/api/users', async () => {
        await gate
        return HttpResponse.json({ items: [], total: 0 })
      }),
    )

    const { wrapper } = await renderSection()
    expect(wrapper.text()).toContain(t('common.loading'))

    release()
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.minors.empty')))
  })

  it('shows the empty message when there is no signed-in user to load minors for', async () => {
    const { wrapper } = await renderWithProviders(MinorsSection)

    expect(wrapper.text()).toContain(t('pages.account.minors.empty'))
  })

  it('lists each minor with birth date and gender', async () => {
    serveChildren()

    const { wrapper } = await renderSection()

    expect(wrapper.text()).toContain(t('pages.account.minors.title'))
    expect(minorItem('Byron Lovelace').querySelector('.acc-minor__meta')?.textContent.trim()).toBe(
      `${formatDate('2015-03-02')} · ${t(genderLabelKey('Male'))}`,
    )
    expect(minorItem('Anne King').querySelector('.acc-minor__meta')?.textContent.trim()).toBe(
      `${formatDate('2018-11-20')} · ${t(genderLabelKey('Female'))}`,
    )
  })

  it('adds a minor, confirms it and reloads the list', async () => {
    const children = serveChildren([])
    let received: { path: string; body: unknown } | undefined
    server.use(
      http.post('/api/users/:userId/children', async ({ request }) => {
        received = { path: new URL(request.url).pathname, body: await request.json() }
        return HttpResponse.json(BYRON, { status: 201 })
      }),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('pages.account.minors.add'), document.body))
    const dialog = openDialog(t('pages.account.minors.addHeader'))
    await typeInto('#m-firstname', '  Byron ', dialog)
    await typeInto('#m-lastname', ' Lovelace ', dialog)
    await typeInto('#m-dob', '2015-03-02', dialog)
    await selectGender(wrapper, 'Male')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1/children',
      body: { firstName: 'Byron', lastName: 'Lovelace', birthDate: '2015-03-02', gender: 'Male' },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.minors.addedSummary'))
    await vi.waitFor(() => expect(children.count()).toBe(2))
  })

  it('requires a gender before saving a minor', async () => {
    serveChildren([])
    const posted = vi.fn()
    server.use(
      http.post('/api/users/:userId/children', () => {
        posted()
        return HttpResponse.json(BYRON)
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.minors.add'), document.body))
    const dialog = openDialog(t('pages.account.minors.addHeader'))
    await fillMinor(dialog)
    expect(dialog.textContent).not.toContain(t('entities.user.person.genderRequired'))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('entities.user.person.genderRequired'))
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(posted).not.toHaveBeenCalled()
  })

  it('shows the API error when a minor cannot be added and keeps the dialog open', async () => {
    serveChildren([])
    server.use(http.post('/api/users/:userId/children', () => apiError(404, 'ParentUserNotFound')))
    const { wrapper } = await renderSection()

    await click(findButton(t('pages.account.minors.add'), document.body))
    const dialog = openDialog(t('pages.account.minors.addHeader'))
    await fillMinor(dialog)
    await selectGender(wrapper, 'Female')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.ParentUserNotFound'))
    expect(openDialogs()).toHaveLength(1)
  })

  it('edits a minor with the form prefilled and keeps the parent link', async () => {
    serveChildren()
    let received: { path: string; body: unknown } | undefined
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        received = { path: new URL(request.url).pathname, body: await request.json() }
        return HttpResponse.json(BYRON)
      }),
    )
    await renderSection()

    await click(findButton(t('common.edit'), minorItem('Byron Lovelace')))
    const dialog = openDialog(t('pages.account.minors.editHeader'))
    expect(dialog.querySelector<HTMLInputElement>('#m-firstname')?.value).toBe('Byron')
    expect(dialog.querySelector<HTMLInputElement>('#m-dob')?.value).toBe('2015-03-02')
    await typeInto('#m-firstname', 'Lord Byron', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/child-1',
      body: {
        firstName: 'Lord Byron',
        lastName: 'Lovelace',
        birthDate: '2015-03-02',
        gender: 'Male',
        promotionalConsent: false,
        parentId: 'user-1',
      },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.minors.updatedSummary'))
  })

  it('refuses to turn a minor into an adult but keeps the stored birth date of one who came of age', async () => {
    const grownUp = buildDependentResponse({
      id: 'child-3',
      firstName: 'Tom',
      birthDate: '2000-03-04',
    })
    serveChildren([BYRON, grownUp])
    const bodies: unknown[] = []
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(grownUp)
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.minors.add'), document.body))
    const addDialog = openDialog(t('pages.account.minors.addHeader'))
    expect(addDialog.querySelector('#m-dob')?.getAttribute('min')).toBe(minorBirthDateRange().min)
    await click(findButton(t('common.cancel'), addDialog))

    await click(findButton(t('common.edit'), minorItem('Byron Lovelace')))
    const editDialog = openDialog(t('pages.account.minors.editHeader'))
    expect(editDialog.querySelector('#m-dob')?.getAttribute('min')).toBeNull()
    await typeInto('#m-dob', '1999-05-05', editDialog)
    await click(findButton(t('common.save'), editDialog))

    expect(editDialog.textContent).toContain(t('entities.user.person.birthDateNotMinor'))
    expect(bodies).toHaveLength(0)
    await click(findButton(t('common.cancel'), editDialog))

    await click(findButton(t('common.edit'), minorItem('Tom Lovelace')))
    await typeInto('#m-firstname', 'Thomas', editDialog)
    await click(findButton(t('common.save'), editDialog))

    await vi.waitFor(() => expect(bodies).toHaveLength(1))
    expect(bodies[0]).toMatchObject({
      firstName: 'Thomas',
      birthDate: '2000-03-04',
      parentId: 'user-1',
    })
  })

  it('notifies when a minor cannot be updated', async () => {
    serveChildren()
    server.use(http.put('/api/users/:userId', () => apiError(500)))
    await renderSection()

    await click(findButton(t('common.edit'), minorItem('Byron Lovelace')))
    await click(findButton(t('common.save'), openDialog(t('pages.account.minors.editHeader'))))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.generic'))
  })

  it('closes the form without saving when cancelled or dismissed', async () => {
    serveChildren()
    const { wrapper } = await renderSection()

    await click(findButton(t('pages.account.minors.add'), document.body))
    await click(findButton(t('common.cancel'), openDialog(t('pages.account.minors.addHeader'))))
    expect(openDialogs()).toHaveLength(0)

    await click(findButton(t('common.edit'), minorItem('Byron Lovelace')))
    const formDialog = wrapper
      .findAllComponents(ElDialog)
      .find((dialog) => dialog.props('title') === t('pages.account.minors.editHeader'))
    formDialog?.vm.$emit('update:modelValue', false)
    await flushPromises()
    expect(openDialogs()).toHaveLength(0)
  })

  it('deletes a minor after confirmation', async () => {
    const children = serveChildren()
    let deletedPath: string | undefined
    server.use(
      http.delete('/api/users/:userId', ({ request }) => {
        deletedPath = new URL(request.url).pathname
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('common.delete'), minorItem('Anne King')))
    const dialog = openDialog(t('pages.account.minors.deleteHeader'))
    expect(dialog.querySelector('b')?.textContent.trim()).toBe('Anne King')
    await click(findButton(t('common.delete'), dialog))

    await vi.waitFor(() => expect(deletedPath).toBe('/api/users/child-2'))
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.minors.deletedSummary'))
    await vi.waitFor(() => expect(children.count()).toBe(2))
  })

  it('keeps the minor when deletion is cancelled or dismissed', async () => {
    serveChildren()
    const deleted = vi.fn()
    server.use(
      http.delete('/api/users/:userId', () => {
        deleted()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('common.delete'), minorItem('Anne King')))
    await click(findButton(t('common.cancel'), openDialog(t('pages.account.minors.deleteHeader'))))
    expect(openDialogs()).toHaveLength(0)

    await click(findButton(t('common.delete'), minorItem('Anne King')))
    const deleteDialog = wrapper
      .findAllComponents(ElDialog)
      .find((dialog) => dialog.props('title') === t('pages.account.minors.deleteHeader'))
    deleteDialog?.vm.$emit('update:modelValue', true)
    await flushPromises()
    expect(openDialogs()).toHaveLength(1)
    deleteDialog?.vm.$emit('update:modelValue', false)
    await flushPromises()

    expect(openDialogs()).toHaveLength(0)
    expect(deleted).not.toHaveBeenCalled()
  })

  it('notifies when a minor cannot be deleted', async () => {
    serveChildren()
    server.use(http.delete('/api/users/:userId', () => apiError(404, 'UserNotFound')))
    await renderSection()

    await click(findButton(t('common.delete'), minorItem('Byron Lovelace')))
    await click(findButton(t('common.delete'), openDialog(t('pages.account.minors.deleteHeader'))))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.UserNotFound'))
    expect(openDialogs()).toHaveLength(1)
  })
})
