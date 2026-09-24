import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ResourceFormDialog } from '@/features/manage-resources'
import type { ResourceResponse, ResourceTypeResponse } from '@/shared/api/generated/models'
import RichTextEditor from '@/shared/ui/RichTextEditor.vue'

import {
  buildResource,
  externalType,
  internalType,
  richText,
} from '../../../support/fixtures/admin-content/builders'
import {
  click,
  findButton,
  inputValue,
  openDialog,
  pickFiles,
  typeInto,
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveTypes(types: ResourceTypeResponse[] = [internalType, externalType]) {
  server.use(
    http.get('/api/resources/types', () => HttpResponse.json(types)),
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'cover', extension: '.png' })),
  )
}

async function renderDialog(resource: ResourceResponse | null = null) {
  const rendered = await renderWithProviders(ResourceFormDialog, {
    props: { visible: false, resource, saving: false },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return rendered
}

function title(resource: ResourceResponse | null): string {
  return resource
    ? t('features.manageResources.editHeader')
    : t('features.manageResources.newHeader')
}

async function fillBasics(
  wrapper: Awaited<ReturnType<typeof renderDialog>>['wrapper'],
  typeId: string,
) {
  const dialog = openDialog(title(null))
  await typeInto('#resource-title', '  Vue docs ')
  await typeInto('#resource-subtitle', ' Official ')
  wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', typeId)
  await flushPromises()
  const input = dialog.querySelector<HTMLInputElement>('.thumb input[type="file"]')
  if (!input) throw new Error('missing file input')
  await pickFiles(input, [new File(['img'], 'cover.png', { type: 'image/png' })])
  return dialog
}

describe('ResourceFormDialog', () => {
  it('requires every field before submitting', async () => {
    serveTypes()
    const { wrapper } = await renderDialog()
    const dialog = openDialog(title(null))

    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageResources.typeRequired'))
    expect(dialog.textContent).toContain(t('common.imageRequired'))
    expect(dialog.querySelectorAll('.ca-invalid').length).toBeGreaterThan(0)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('submits an internal resource with its rich-text description and uploaded image', async () => {
    serveTypes()
    server.use(http.post('/api/files', () => HttpResponse.json({ id: 'uploaded-id' })))
    const { wrapper } = await renderDialog()
    const dialog = await fillBasics(wrapper, 'type-article')

    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageResources.descriptionRequired'))
    expect(wrapper.findComponent(RichTextEditor).props('invalid')).toBe(true)
    expect(wrapper.emitted('submit')).toBeUndefined()

    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('Body'))
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      title: 'Vue docs',
      subtitle: 'Official',
      description: richText('Body'),
      url: null,
      resourceTypeId: 'type-article',
      thumbnailId: 'uploaded-id',
    })
  })

  it('validates and submits the url of an external resource', async () => {
    serveTypes()
    server.use(http.post('/api/files', () => HttpResponse.json({ id: 'uploaded-id' })))
    const { wrapper } = await renderDialog()
    const dialog = await fillBasics(wrapper, 'type-link')
    expect(wrapper.findComponent(RichTextEditor).exists()).toBe(false)

    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageResources.urlRequired'))

    await typeInto('#resource-url', 'ftp://example.test')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageResources.urlInvalid'))

    await typeInto('#resource-url', 'https://')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageResources.urlInvalid'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#resource-url', ' https://vuejs.org ')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      title: 'Vue docs',
      subtitle: 'Official',
      description: null,
      url: 'https://vuejs.org',
      resourceTypeId: 'type-link',
      thumbnailId: 'uploaded-id',
    })
  })

  it('populates the form when editing and keeps the existing image', async () => {
    serveTypes()
    const resource = buildResource()
    const { wrapper } = await renderDialog(resource)
    const dialog = openDialog(title(resource))

    expect(inputValue('#resource-title')).toBe('Vue guide')
    expect(inputValue('#resource-subtitle')).toBe('Getting started')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(wrapper.emitted('submit')).toHaveLength(1))
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      resourceTypeId: 'type-article',
      thumbnailId: 'thumb-resource-1',
      description: richText('Hello'),
    })
  })

  it('does not submit when the resource type is no longer available', async () => {
    serveTypes([externalType])
    const resource = buildResource()
    const { wrapper } = await renderDialog(resource)

    await click(findButton(t('common.save'), openDialog(title(resource))))

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('shows a types load error and retries when reopened', async () => {
    let calls = 0
    server.use(
      http.get('/api/resources/types', () => {
        calls += 1
        // The query runs on mount and is retried when the dialog opens; both attempts fail.
        return calls <= 2 ? apiError(500) : HttpResponse.json([internalType])
      }),
    )
    const { wrapper } = await renderDialog()

    await vi.waitFor(() =>
      expect(openDialog(title(null)).textContent).toContain(
        t('features.manageResources.typesLoadError'),
      ),
    )

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    await vi.waitFor(() => expect(calls).toBe(3))
    await vi.waitFor(() =>
      expect(openDialog(title(null)).textContent).not.toContain(
        t('features.manageResources.typesLoadError'),
      ),
    )
  })

  it('shows the upload error and does not submit when the image upload fails', async () => {
    serveTypes()
    server.use(http.post('/api/files', () => apiError(500)))
    const { wrapper } = await renderDialog()
    const dialog = await fillBasics(wrapper, 'type-link')
    await typeInto('#resource-url', 'https://vuejs.org')

    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(t('entities.file.thumbnail.uploadFailed')),
    )
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('lists incomplete resource types with empty labels', async () => {
    serveTypes([internalType, { isExternal: true }])
    const { wrapper } = await renderDialog()

    const options = wrapper.findComponent(ElSelect).findAllComponents({ name: 'ElOption' })
    expect(options.map((option) => [option.props('label'), option.props('value')])).toEqual([
      ['Artículo', 'type-article'],
      ['', ''],
    ])
  })

  it('emits a cleared visibility when cancelled', async () => {
    serveTypes()
    const { wrapper } = await renderDialog()

    await click(findButton(t('common.cancel'), openDialog(title(null))))

    expect(wrapper.emitted('update:visible')).toEqual([[false]])
  })
})
