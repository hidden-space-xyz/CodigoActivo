import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDatePicker, ElDialog, ElInputNumber, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import type { ActivityDetail } from '@/entities/activity'
import { ActivityFormDialog } from '@/features/manage-activities'
import type { CreateActivityRequest } from '@/shared/api/generated/models'

import {
  bodyFind,
  clickBodyButton,
  modalityTypes,
  pickFile,
  propOf,
  roleTypes,
  THUMBNAIL_ID,
  useCatalogHandlers,
} from '../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

const detail: ActivityDetail = {
  id: 'act-1',
  title: 'Robotics',
  description: 'Build robots',
  location: 'Room 1',
  modalityId: 'mod-1',
  startsAt: '2026-10-10T09:00:00.000Z',
  endsAt: '2026-10-10T11:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  roleCapacities: [{ roleTypeId: 'role-1', desiredCount: 4 }],
}

async function openDialog(props: Record<string, unknown> = {}) {
  useCatalogHandlers()
  const rendered = await renderWithProviders(ActivityFormDialog, {
    props: {
      visible: false,
      activity: null,
      modalityTypes,
      roleTypes,
      saving: false,
      eventStart: '2026-10-10',
      eventEnd: '2026-10-12',
      ...props,
    },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return rendered
}

function datePicker(wrapper: VueWrapper, index: number) {
  const picker = wrapper.findAllComponents(ElDatePicker)[index]
  if (!picker) throw new Error(`Date picker ${index} not found`)
  return picker
}

async function setDates(wrapper: VueWrapper, start: Date | null, end: Date | null) {
  datePicker(wrapper, 0).vm.$emit('update:modelValue', start)
  datePicker(wrapper, 1).vm.$emit('update:modelValue', end)
  await flushPromises()
}

function save() {
  const dialog = document.body.querySelector('.el-dialog')
  if (!dialog) throw new Error('Dialog not found')
  clickBodyButton(t('common.save'), dialog)
}

function bodyText() {
  return document.body.textContent ?? ''
}

describe('ActivityFormDialog', () => {
  it('uploads the thumbnail and emits a trimmed create request with positive role targets', async () => {
    let uploads = 0
    server.use(
      http.post('/api/files', () => {
        uploads += 1
        return HttpResponse.json({ id: 'thumb-new' }, { status: 201 })
      }),
    )
    const { wrapper } = await openDialog()
    expect(bodyText()).toContain(t('features.manageActivities.newHeader'))

    await bodyFind<HTMLInputElement>('#activity-title').setValue(' Robotics ')
    await bodyFind<HTMLTextAreaElement>('#activity-description').setValue(' Build robots ')
    await bodyFind<HTMLInputElement>('#activity-location').setValue(' Room 1 ')
    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'mod-2')
    const start = new Date(2026, 9, 10, 9, 0)
    const end = new Date(2026, 9, 10, 11, 0)
    await setDates(wrapper, start, end)
    const counts = wrapper.findAllComponents(ElInputNumber)
    expect(counts).toHaveLength(2)
    counts[0]?.vm.$emit('update:modelValue', 5)
    counts[1]?.vm.$emit('update:modelValue', undefined)
    pickFile(new File(['png'], 'robot.png', { type: 'image/png' }))
    await flushPromises()

    save()
    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))

    expect(uploads).toBe(1)
    expect(wrapper.emitted<[CreateActivityRequest]>('submit')?.[0]?.[0]).toEqual({
      title: 'Robotics',
      description: 'Build robots',
      location: 'Room 1',
      activityModalityTypeId: 'mod-2',
      activityStartsAt: start.toISOString(),
      activityEndsAt: end.toISOString(),
      thumbnailId: 'thumb-new',
      roleCapacities: [{ activityRoleTypeId: 'role-1', desiredCount: 5 }],
    })
  })

  it('repopulates the edited activity and keeps its saved role targets and thumbnail', async () => {
    const { wrapper } = await openDialog({ activity: detail, eventStart: null, eventEnd: null })
    expect(bodyText()).toContain(t('features.manageActivities.editHeader'))
    expect(bodyFind<HTMLInputElement>('#activity-title').element.value).toBe('Robotics')
    expect(wrapper.findAllComponents(ElInputNumber)[0]?.props('modelValue')).toBe(4)

    save()
    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))

    expect(wrapper.emitted<[CreateActivityRequest]>('submit')?.[0]?.[0]).toEqual({
      title: 'Robotics',
      description: 'Build robots',
      location: 'Room 1',
      activityModalityTypeId: 'mod-1',
      activityStartsAt: '2026-10-10T09:00:00.000Z',
      activityEndsAt: '2026-10-10T11:00:00.000Z',
      thumbnailId: THUMBNAIL_ID,
      roleCapacities: [{ activityRoleTypeId: 'role-1', desiredCount: 4 }],
    })
  })

  it('refreshes role targets when the role catalog arrives while open', async () => {
    const { wrapper } = await openDialog({ activity: detail, roleTypes: [] })
    expect(wrapper.findAllComponents(ElInputNumber)).toHaveLength(0)

    await wrapper.setProps({ roleTypes: [...roleTypes, { name: 'No id' }] })
    await flushPromises()

    const inputs = wrapper.findAllComponents(ElInputNumber)
    expect(inputs).toHaveLength(3)
    expect(inputs[0]?.props('modelValue')).toBe(4)
    expect(inputs[1]?.props('modelValue')).toBeUndefined()
  })

  it('ignores role catalog changes while closed and repopulates when the activity changes', async () => {
    const { wrapper } = await openDialog({ activity: detail })
    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ roleTypes: [roleTypes[0]] })
    await wrapper.setProps({ visible: true, activity: { ...detail, roleCapacities: [] } })
    await flushPromises()

    expect(wrapper.findAllComponents(ElInputNumber)[0]?.props('modelValue')).toBeUndefined()

    await wrapper.setProps({
      activity: { ...detail, title: 'Painting', startsAt: null, endsAt: null },
    })
    await flushPromises()
    expect(bodyFind<HTMLInputElement>('#activity-title').element.value).toBe('Painting')
    expect(datePicker(wrapper, 0).props('modelValue')).toBeNull()
  })

  it('shows the required-field errors and sends no role capacities without roles', async () => {
    const { wrapper } = await openDialog({ roleTypes: [] })

    save()
    await flushPromises()

    const text = bodyText()
    expect(text).toContain(t('features.manageActivities.errors.modalityRequired'))
    expect(text).toContain(t('features.manageActivities.errors.locationRequired'))
    expect(text).toContain(t('features.manageActivities.errors.startRequired'))
    expect(text).toContain(t('features.manageActivities.errors.endRequired'))
    expect(text).toContain(t('common.imageRequired'))
    expect(text).not.toContain(t('features.manageActivities.fields.desiredCounts'))
    expect(bodyFind('#activity-title').element.closest('.ca-invalid')).not.toBeNull()
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('rejects an end before the start and a schedule outside the event days', async () => {
    const { wrapper } = await openDialog({ activity: detail })

    await setDates(wrapper, new Date(2026, 9, 10, 12, 0), new Date(2026, 9, 10, 10, 0))
    save()
    await flushPromises()
    expect(bodyText()).toContain(t('features.manageActivities.errors.orderInvalid'))
    expect(bodyText()).not.toContain(t('features.manageActivities.errors.outsideEvent'))

    await setDates(wrapper, new Date(2026, 9, 9, 10, 0), new Date(2026, 9, 10, 10, 0))
    expect(bodyText()).toContain(t('features.manageActivities.errors.outsideEvent'))

    await setDates(wrapper, new Date(2026, 9, 12, 10, 0), new Date(2026, 9, 13, 10, 0))
    expect(bodyText()).toContain(t('features.manageActivities.errors.outsideEvent'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('disables days outside the event and before the chosen start', async () => {
    const { wrapper } = await openDialog()
    const disabled = (index: number, day: Date) =>
      (propOf(datePicker(wrapper, index), 'disabledDate') as (date: Date) => boolean)(day)

    expect(disabled(0, new Date(2026, 9, 9))).toBe(true)
    expect(disabled(0, new Date(2026, 9, 13))).toBe(true)
    expect(disabled(0, new Date(2026, 9, 11))).toBe(false)
    expect(disabled(1, new Date(2026, 9, 13))).toBe(true)
    expect(disabled(1, new Date(2026, 9, 10))).toBe(false)

    await setDates(wrapper, new Date(2026, 9, 11, 9, 0), null)
    expect(disabled(1, new Date(2026, 9, 10))).toBe(true)
    expect(disabled(1, new Date(2026, 9, 11))).toBe(false)
  })

  it('allows any day when the event dates are unknown', async () => {
    const { wrapper } = await openDialog({ eventStart: undefined, eventEnd: undefined })
    const disabled = propOf(datePicker(wrapper, 0), 'disabledDate') as (date: Date) => boolean

    expect(disabled(new Date(2000, 0, 1))).toBe(false)
  })

  it('shows the upload error and does not submit when the thumbnail cannot be uploaded', async () => {
    server.use(http.put('/api/files/:fileId', () => apiError(500)))
    const { wrapper } = await openDialog({ activity: detail })
    pickFile(new File(['png'], 'robot.png', { type: 'image/png' }))
    await flushPromises()

    save()

    await vi.waitFor(() => expect(bodyText()).toContain(t('entities.file.thumbnail.uploadFailed')))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits update:visible false when cancelled or dismissed', async () => {
    const { wrapper } = await openDialog()

    const dialog = document.body.querySelector('.el-dialog')
    if (!dialog) throw new Error('Dialog not found')
    clickBodyButton(t('common.cancel'), dialog)
    wrapper.findComponent(ElDialog).vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('update:visible')).toEqual([[false], [false]])
  })
})
