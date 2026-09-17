import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElColorPicker, ElDatePicker, ElDialog, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { EventFormDialog } from '@/features/manage-events'
import type {
  CreateEventRequest,
  CreateEventCategoryTypeRequest,
  EventResponse,
} from '@/shared/api/generated/models'
import RichTextEditor from '@/shared/ui/RichTextEditor.vue'

import {
  bodyFind,
  buildEvent,
  clickBodyButton,
  pickFile,
  propOf,
  THUMBNAIL_ID,
  useCatalogHandlers,
} from '../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

async function openDialog(event: EventResponse | null = null) {
  useCatalogHandlers()
  const rendered = await renderWithProviders(EventFormDialog, {
    props: { visible: false, event, saving: false },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return rendered
}

function pickers(wrapper: VueWrapper) {
  const all = wrapper.findAllComponents(ElDatePicker)
  const at = (index: number) => {
    const picker = all[index]
    if (!picker) throw new Error(`Date picker ${index} not found`)
    return picker
  }
  return {
    eventStart: at(0),
    eventEnd: at(1),
    earlySignup: at(2),
    signupStart: at(3),
    signupEnd: at(4),
  }
}

async function setDate(picker: VueWrapper, value: Date | null) {
  picker.vm.$emit('update:modelValue', value)
  await flushPromises()
}

async function fillValidForm(wrapper: VueWrapper) {
  await bodyFind<HTMLInputElement>('#event-title').setValue('  Hackathon  ')
  await bodyFind<HTMLInputElement>('#event-subtitle').setValue(' Code all night ')
  wrapper.findAllComponents(ElSelect)[0]?.vm.$emit('update:modelValue', ['cat-2'])
  const dates = pickers(wrapper)
  await setDate(dates.eventStart, new Date(2026, 9, 10))
  await setDate(dates.eventEnd, new Date(2026, 9, 12))
  await setDate(dates.signupStart, new Date(2026, 8, 1, 9, 0))
  await setDate(dates.signupEnd, new Date(2026, 9, 1, 20, 0))
}

function saveButton() {
  const footer = [...document.body.querySelectorAll('.el-dialog')].find((dialog) =>
    dialog.textContent?.includes(t('features.manageEvents.newHeader')),
  )
  if (!footer) throw new Error('Event dialog not found')
  return footer
}

function bodyText() {
  return document.body.textContent ?? ''
}

describe('EventFormDialog', () => {
  it('uploads the picked thumbnail and emits a normalized create request', async () => {
    let uploaded: FormDataEntryValue | null = null
    server.use(
      http.post('/api/files', async ({ request }) => {
        uploaded = (await request.formData()).get('file')
        return HttpResponse.json({ id: 'new-thumb' }, { status: 201 })
      }),
    )
    const { wrapper } = await openDialog()
    expect(bodyText()).toContain(t('features.manageEvents.newHeader'))

    await fillValidForm(wrapper)
    wrapper.findAllComponents(ElSelect)[1]?.vm.$emit('update:modelValue', ['terms-1'])
    pickFile(new File(['png'], 'cover.png', { type: 'image/png' }))
    await flushPromises()

    clickBodyButton(t('common.save'), saveButton())
    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))

    expect(uploaded).toMatchObject({ name: 'cover.png', type: 'image/png' })
    const [body] = wrapper.emitted<[CreateEventRequest]>('submit')?.[0] ?? []
    expect(body).toEqual({
      title: 'Hackathon',
      subtitle: 'Code all night',
      description: '{"type":"doc","content":[]}',
      categoryTypeIds: ['cat-2'],
      eventStartsAt: '2026-10-10',
      eventEndsAt: '2026-10-12',
      earlySignupStartsAt: null,
      signupStartsAt: new Date(2026, 8, 1, 9, 0).toISOString(),
      signupEndsAt: new Date(2026, 9, 1, 20, 0).toISOString(),
      thumbnailId: 'new-thumb',
      termsDocuments: [{ termsDocumentId: 'terms-1', required: true }],
    })
  })

  it('repopulates the form from the edited event and keeps its thumbnail and description', async () => {
    const event = buildEvent({
      earlySignupStartsAt: '2026-08-20T08:00:00.000Z',
      description: '{"type":"doc","content":[{"type":"paragraph"}]}',
    })
    const { wrapper } = await openDialog(event)
    expect(bodyText()).toContain(t('features.manageEvents.editHeader'))
    expect(bodyFind<HTMLInputElement>('#event-title').element.value).toBe('Hackathon')

    const editor = wrapper.findComponent(RichTextEditor)
    editor.vm.$emit('update:modelValue', '{"type":"doc","content":[{"type":"text"}]}')
    await flushPromises()

    const dialog = [...document.body.querySelectorAll('.el-dialog')].find((node) =>
      node.textContent?.includes(t('features.manageEvents.editHeader')),
    )
    if (!dialog) throw new Error('Dialog not found')
    clickBodyButton(t('common.save'), dialog)
    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))

    const [body] = wrapper.emitted<[CreateEventRequest]>('submit')?.[0] ?? []
    expect(body).toMatchObject({
      title: 'Hackathon',
      description: '{"type":"doc","content":[{"type":"text"}]}',
      categoryTypeIds: ['cat-1'],
      eventStartsAt: '2026-10-10',
      eventEndsAt: '2026-10-12',
      earlySignupStartsAt: '2026-08-20T08:00:00.000Z',
      signupStartsAt: '2026-09-01T08:00:00.000Z',
      signupEndsAt: '2026-10-01T20:00:00.000Z',
      thumbnailId: THUMBNAIL_ID,
      termsDocuments: [{ termsDocumentId: 'terms-1', required: true }],
    })
  })

  it('populates an event without optional data with empty defaults', async () => {
    const { wrapper } = await openDialog(
      buildEvent({
        title: null,
        subtitle: null,
        description: null,
        categories: null,
        termsDocuments: undefined,
        signupStartsAt: '',
        signupEndsAt: '',
        eventStartsAt: '',
        eventEndsAt: '',
        thumbnailId: '',
      }),
    )

    expect(bodyFind<HTMLInputElement>('#event-title').element.value).toBe('')
    expect(wrapper.findAllComponents(ElSelect)[0]?.props('modelValue')).toEqual([])
    expect(pickers(wrapper).eventStart.props('modelValue')).toBeNull()
  })

  it('shows every required-field error and emits nothing when the form is empty', async () => {
    const { wrapper } = await openDialog()

    clickBodyButton(t('common.save'), saveButton())
    await flushPromises()

    const text = bodyText()
    expect(text).toContain(t('features.manageEvents.errors.categoriesRequired'))
    expect(text).toContain(t('features.manageEvents.errors.eventStartRequired'))
    expect(text).toContain(t('features.manageEvents.errors.eventEndRequired'))
    expect(text).toContain(t('features.manageEvents.errors.signupStartRequired'))
    expect(text).toContain(t('features.manageEvents.errors.signupEndRequired'))
    expect(text).toContain(t('common.imageRequired'))
    expect(bodyFind('#event-title').element.closest('.ca-invalid')).not.toBeNull()
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('rejects inconsistent date ranges', async () => {
    const { wrapper } = await openDialog(buildEvent())
    const dates = pickers(wrapper)
    await setDate(dates.eventStart, new Date(2026, 9, 12))
    await setDate(dates.eventEnd, new Date(2026, 9, 10))
    await setDate(dates.signupStart, new Date(2026, 9, 20, 10, 0))
    await setDate(dates.signupEnd, new Date(2026, 9, 20, 10, 0))
    await setDate(dates.earlySignup, new Date(2026, 9, 21, 10, 0))

    const dialog = document.body.querySelector('.el-dialog')
    if (!dialog) throw new Error('Dialog not found')
    clickBodyButton(t('common.save'), dialog)
    await flushPromises()

    const text = bodyText()
    expect(text).toContain(t('features.manageEvents.errors.eventOrderInvalid'))
    expect(text).toContain(t('features.manageEvents.errors.signupOrderInvalid'))
    expect(text).toContain(t('features.manageEvents.errors.signupAfterEventEnd'))
    expect(text).toContain(t('features.manageEvents.errors.earlySignupOrderInvalid'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('disables calendar days outside the allowed ranges', async () => {
    const { wrapper } = await openDialog()
    const dates = pickers(wrapper)
    const disabled = (picker: VueWrapper, day: Date) =>
      (propOf(picker, 'disabledDate') as (date: Date) => boolean)(day)

    expect(disabled(dates.eventEnd, new Date(2026, 0, 1))).toBe(false)
    expect(disabled(dates.earlySignup, new Date(2030, 0, 1))).toBe(false)
    expect(disabled(dates.signupEnd, new Date(2020, 0, 1))).toBe(false)

    await setDate(dates.eventStart, new Date(2026, 9, 10))
    await setDate(dates.signupStart, new Date(2026, 8, 1, 9, 0))

    expect(disabled(dates.eventEnd, new Date(2026, 9, 9))).toBe(true)
    expect(disabled(dates.eventEnd, new Date(2026, 9, 10))).toBe(false)
    expect(disabled(dates.earlySignup, new Date(2026, 8, 2))).toBe(true)
    expect(disabled(dates.earlySignup, new Date(2026, 8, 1))).toBe(false)
    expect(disabled(dates.signupEnd, new Date(2026, 7, 31))).toBe(true)
    expect(disabled(dates.signupEnd, new Date(2026, 8, 1))).toBe(false)
  })

  it('shows the upload error and does not submit when the thumbnail upload fails', async () => {
    let uploads = 0
    server.use(
      http.post('/api/files', () => {
        uploads += 1
        return apiError(500)
      }),
    )
    const { wrapper } = await openDialog()
    await fillValidForm(wrapper)
    pickFile(new File(['png'], 'cover.png', { type: 'image/png' }))
    await flushPromises()

    clickBodyButton(t('common.save'), saveButton())
    await vi.waitFor(() => expect(bodyText()).toContain(t('entities.file.thumbnail.uploadFailed')))
    expect(uploads).toBe(1)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits update:visible false when cancelled or dismissed', async () => {
    const { wrapper } = await openDialog()

    clickBodyButton(t('common.cancel'), saveButton())
    await flushPromises()
    wrapper.findComponent(ElDialog).vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('update:visible')).toEqual([[false], [false]])
  })

  it('creates a category inline and selects it', async () => {
    const received: CreateEventCategoryTypeRequest[] = []
    server.use(
      http.post('/api/events/categoryType', async ({ request }) => {
        received.push((await request.json()) as CreateEventCategoryTypeRequest)
        return HttpResponse.json({ id: 'cat-9', name: 'Music', color: '#ABCDEF' }, { status: 201 })
      }),
    )
    const { wrapper } = await openDialog()

    clickBodyButton(t('features.manageEvents.newCategory'))
    await flushPromises()
    expect(bodyText()).toContain(t('features.manageEvents.categoryDialog.header'))

    const catDialog = () => {
      const dialog = [...document.body.querySelectorAll('.el-dialog')].find((node) =>
        node.textContent?.includes(t('features.manageEvents.categoryDialog.header')),
      )
      if (!dialog) throw new Error('Category dialog not found')
      return dialog
    }

    clickBodyButton(t('common.create'), catDialog())
    await flushPromises()
    expect(received).toHaveLength(0)
    expect(bodyFind('#event-category-new-name').element.closest('.ca-invalid')).not.toBeNull()

    const picker = wrapper.findComponent(ElColorPicker)
    picker.vm.$emit('update:modelValue', null)
    await flushPromises()
    expect(catDialog().textContent).toContain('#6366F1')
    picker.vm.$emit('update:modelValue', 'ABCDEF')
    await flushPromises()
    expect(catDialog().textContent).toContain('#ABCDEF')

    await bodyFind<HTMLInputElement>('#event-category-new-name').setValue('  Music ')
    clickBodyButton(t('common.create'), catDialog())
    await vi.waitFor(() => expect(received).toHaveLength(1))
    await flushPromises()

    expect(received[0]).toEqual({ name: 'Music', color: '#ABCDEF' })
    expect(wrapper.findAllComponents(ElSelect)[0]?.props('modelValue')).toEqual(['cat-9'])
  })

  it('does not duplicate an already selected category returned by the API', async () => {
    server.use(
      http.post('/api/events/categoryType', () =>
        HttpResponse.json({ id: 'cat-1', name: 'Tech' }, { status: 201 }),
      ),
    )
    const { wrapper } = await openDialog(buildEvent())

    clickBodyButton(t('features.manageEvents.newCategory'))
    await flushPromises()
    await bodyFind<HTMLInputElement>('#event-category-new-name').setValue('Tech')
    bodyFind('#event-category-new-name').element.closest('form')?.requestSubmit()
    await vi.waitFor(() =>
      expect(wrapper.findAllComponents(ElSelect)[0]?.props('modelValue')).toEqual(['cat-1']),
    )
  })

  it('shows the translated error when the category cannot be created', async () => {
    server.use(http.post('/api/events/categoryType', () => apiError(500)))
    const { wrapper } = await openDialog()

    clickBodyButton(t('features.manageEvents.newCategory'))
    await flushPromises()
    await bodyFind<HTMLInputElement>('#event-category-new-name').setValue('Music')
    const dialog = [...document.body.querySelectorAll('.el-dialog')].find((node) =>
      node.textContent?.includes(t('features.manageEvents.categoryDialog.header')),
    )
    if (!dialog) throw new Error('Category dialog not found')
    clickBodyButton(t('common.create'), dialog)

    await vi.waitFor(() =>
      expect(bodyText()).toContain(t('features.manageEvents.categoryDialog.createError')),
    )

    clickBodyButton(t('common.cancel'), dialog)
    await flushPromises()
    const categoryDialog = wrapper.findAllComponents(ElDialog)[1]
    expect(categoryDialog?.props('modelValue')).toBe(false)

    clickBodyButton(t('features.manageEvents.newCategory'))
    await flushPromises()
    expect(categoryDialog?.props('modelValue')).toBe(true)
    categoryDialog?.vm.$emit('update:modelValue', false)
    await flushPromises()
    expect(categoryDialog?.props('modelValue')).toBe(false)
    expect(wrapper.emitted('update:visible')).toBeUndefined()
  })
})
