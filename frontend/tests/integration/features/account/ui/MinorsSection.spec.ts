import { flushPromises } from '@vue/test-utils'
import { ElDialog, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import MinorsSection from '@/features/account/ui/MinorsSection.vue'
import { genderLabel } from '@/entities/user'
import type { UserResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import { buildChildResponse, omit } from '../../../../support/fixtures/account/account'
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

const BYRON = buildChildResponse()
const ANNE = omit(
  buildChildResponse({
    id: 'child-2',
    firstName: 'Anne',
    lastName: 'King',
    birthDate: '2018-11-20',
  }),
  'gender',
)

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
  await fill(dialog, '#m-firstname', 'Grace')
  await fill(dialog, '#m-lastname', 'Hopper')
  await fill(dialog, '#m-dob', '2016-12-09')
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
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('features.account.minors.empty')))
  })

  it('shows the empty message when there is no signed-in user to load minors for', async () => {
    const { wrapper } = await renderWithProviders(MinorsSection)

    expect(wrapper.text()).toContain(t('features.account.minors.empty'))
  })

  it('lists each minor with birth date and gender', async () => {
    serveChildren()

    const { wrapper } = await renderSection()

    expect(wrapper.text()).toContain(t('features.account.minors.title'))
    expect(minorItem('Byron Lovelace').querySelector('.acc-minor__meta')?.textContent.trim()).toBe(
      `${formatDate('2015-03-02')} · ${genderLabel('Male')}`,
    )
    expect(minorItem('Anne King').querySelector('.acc-minor__meta')?.textContent.trim()).toBe(
      formatDate('2018-11-20'),
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

    await click(buttonByText(document.body, t('features.account.minors.add')))
    const dialog = dialogByTitle(t('features.account.minors.addHeader'))
    await fill(dialog, '#m-firstname', '  Byron ')
    await fill(dialog, '#m-lastname', ' Lovelace ')
    await fill(dialog, '#m-dob', '2015-03-02')
    await selectGender(wrapper, 'Male')
    await click(buttonByText(dialog, t('common.save')))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/user-1/children',
      body: { firstName: 'Byron', lastName: 'Lovelace', birthDate: '2015-03-02', gender: 'Male' },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('features.account.minors.addedSummary'))
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

    await click(buttonByText(document.body, t('features.account.minors.add')))
    const dialog = dialogByTitle(t('features.account.minors.addHeader'))
    await fillMinor(dialog)
    expect(dialog.textContent).not.toContain(t('validation.genderRequired'))
    await click(buttonByText(dialog, t('common.save')))

    expect(dialog.textContent).toContain(t('validation.genderRequired'))
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(posted).not.toHaveBeenCalled()
  })

  it('shows the API error when a minor cannot be added and keeps the dialog open', async () => {
    serveChildren([])
    server.use(http.post('/api/users/:userId/children', () => apiError(404, 'ParentUserNotFound')))
    const { wrapper } = await renderSection()

    await click(buttonByText(document.body, t('features.account.minors.add')))
    const dialog = dialogByTitle(t('features.account.minors.addHeader'))
    await fillMinor(dialog)
    await selectGender(wrapper, 'Female')
    await click(buttonByText(dialog, t('common.save')))

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

    await click(buttonByText(minorItem('Byron Lovelace'), t('common.edit')))
    const dialog = dialogByTitle(t('features.account.minors.editHeader'))
    expect(dialog.querySelector<HTMLInputElement>('#m-firstname')?.value).toBe('Byron')
    expect(dialog.querySelector<HTMLInputElement>('#m-dob')?.value).toBe('2015-03-02')
    await fill(dialog, '#m-firstname', 'Lord Byron')
    await click(buttonByText(dialog, t('common.save')))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      path: '/api/users/child-1',
      body: {
        firstName: 'Lord Byron',
        lastName: 'Lovelace',
        birthDate: '2015-03-02',
        gender: 'Male',
        parentId: 'user-1',
      },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('features.account.minors.updatedSummary'))
  })

  it('requires choosing a gender when editing a minor without one', async () => {
    serveChildren()
    await renderSection()

    await click(buttonByText(minorItem('Anne King'), t('common.edit')))
    const dialog = dialogByTitle(t('features.account.minors.editHeader'))
    await click(buttonByText(dialog, t('common.save')))

    expect(dialog.textContent).toContain(t('validation.genderRequired'))
  })

  it('notifies when a minor cannot be updated', async () => {
    serveChildren()
    server.use(http.put('/api/users/:userId', () => apiError(500)))
    await renderSection()

    await click(buttonByText(minorItem('Byron Lovelace'), t('common.edit')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.editHeader')), t('common.save')),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.generic'))
  })

  it('does not send an update for a minor without an identifier', async () => {
    serveChildren([omit(buildChildResponse({ firstName: 'Sin', lastName: 'Id' }), 'id')])
    const updated = vi.fn()
    server.use(
      http.put('/api/users/:userId', () => {
        updated()
        return HttpResponse.json(BYRON)
      }),
    )
    await renderSection()

    await click(buttonByText(minorItem('Sin Id'), t('common.edit')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.editHeader')), t('common.save')),
    )

    expect(updated).not.toHaveBeenCalled()
    expect(openDialogs()).toHaveLength(1)
  })

  it('closes the form without saving when cancelled or dismissed', async () => {
    serveChildren()
    const { wrapper } = await renderSection()

    await click(buttonByText(document.body, t('features.account.minors.add')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.addHeader')), t('common.cancel')),
    )
    expect(openDialogs()).toHaveLength(0)

    await click(buttonByText(minorItem('Byron Lovelace'), t('common.edit')))
    const formDialog = wrapper
      .findAllComponents(ElDialog)
      .find((dialog) => dialog.props('title') === t('features.account.minors.editHeader'))
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

    await click(buttonByText(minorItem('Anne King'), t('common.delete')))
    const dialog = dialogByTitle(t('features.account.minors.deleteHeader'))
    expect(dialog.querySelector('b')?.textContent.trim()).toBe('Anne King')
    await click(buttonByText(dialog, t('common.delete')))

    await vi.waitFor(() => expect(deletedPath).toBe('/api/users/child-2'))
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('features.account.minors.deletedSummary'))
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

    await click(buttonByText(minorItem('Anne King'), t('common.delete')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.deleteHeader')), t('common.cancel')),
    )
    expect(openDialogs()).toHaveLength(0)

    await click(buttonByText(minorItem('Anne King'), t('common.delete')))
    const deleteDialog = wrapper
      .findAllComponents(ElDialog)
      .find((dialog) => dialog.props('title') === t('features.account.minors.deleteHeader'))
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

    await click(buttonByText(minorItem('Byron Lovelace'), t('common.delete')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.deleteHeader')), t('common.delete')),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.UserNotFound'))
    expect(openDialogs()).toHaveLength(1)
  })

  it('does not delete a minor without an identifier', async () => {
    serveChildren([omit(buildChildResponse({ firstName: 'Sin', lastName: 'Id' }), 'id')])
    const deleted = vi.fn()
    server.use(
      http.delete('/api/users/:userId', () => {
        deleted()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(buttonByText(minorItem('Sin Id'), t('common.delete')))
    await click(
      buttonByText(dialogByTitle(t('features.account.minors.deleteHeader')), t('common.delete')),
    )

    expect(deleted).not.toHaveBeenCalled()
  })
})
