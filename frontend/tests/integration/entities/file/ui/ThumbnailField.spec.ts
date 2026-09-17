import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { ThumbnailField } from '@/entities/file'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

function image(name = 'poster.png', size?: number): File {
  const file = new File(['bytes'], name, { type: 'image/png' })
  if (size !== undefined) Object.defineProperty(file, 'size', { value: size })
  return file
}

async function pickFiles(wrapper: VueWrapper, files: File[]): Promise<void> {
  const input = wrapper.find('input[type="file"]')
  Object.defineProperty(input.element, 'files', { value: files, configurable: true })
  await input.trigger('change')
}

function buttonLabels(wrapper: VueWrapper): string[] {
  return wrapper.findAll('button').map((button) => button.text())
}

function serveFileNames(names: Record<string, { name?: string; extension?: string }>) {
  const requested: string[] = []
  server.use(
    http.get('/api/files/:fileId', ({ params }) => {
      const id = String(params.fileId)
      requested.push(id)
      const meta = names[id]
      return meta ? HttpResponse.json({ id, ...meta }) : apiError(404, 'FileNotFound')
    }),
  )
  return requested
}

describe('ThumbnailField', () => {
  it('shows the placeholder and a select button without a thumbnail', async () => {
    const { wrapper } = await renderWithProviders(ThumbnailField)

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.thumb__placeholder').text()).toBe(
      t('entities.file.thumbnail.placeholder'),
    )
    expect(buttonLabels(wrapper)).toEqual([t('entities.file.thumbnail.select')])
    expect(wrapper.find('.thumb__name').exists()).toBe(false)
    expect(wrapper.find('.thumb__preview--invalid').exists()).toBe(false)
  })

  it('marks the preview as invalid', async () => {
    const { wrapper } = await renderWithProviders(ThumbnailField, { props: { invalid: true } })

    expect(wrapper.find('.thumb__preview--invalid').exists()).toBe(true)
  })

  it('previews the stored thumbnail and shows its file name', async () => {
    const requested = serveFileNames({ 'file-1': { name: 'poster', extension: '.png' } })

    const { wrapper } = await renderWithProviders(ThumbnailField, {
      props: { existingThumbnailId: 'file-1' },
    })
    await flushPromises()

    expect(wrapper.find('img').attributes()).toMatchObject({
      src: '/api/files/file-1/content',
      alt: t('entities.file.thumbnail.alt'),
    })
    expect(wrapper.find('.thumb__name').text()).toBe('poster.png')
    expect(buttonLabels(wrapper)).toEqual([t('entities.file.thumbnail.change')])
    expect(requested).toEqual(['file-1'])
  })

  it('keeps the preview without a name when the file metadata cannot be loaded', async () => {
    server.use(http.get('/api/files/file-1', () => apiError(500)))

    const { wrapper } = await renderWithProviders(ThumbnailField, {
      props: { existingThumbnailId: 'file-1' },
    })
    await flushPromises()

    expect(wrapper.find('img').attributes('src')).toBe('/api/files/file-1/content')
    expect(wrapper.find('.thumb__name').exists()).toBe(false)
  })

  it('opens the native file picker from the select button', async () => {
    const click = vi.spyOn(HTMLInputElement.prototype, 'click').mockImplementation(() => undefined)
    const { wrapper } = await renderWithProviders(ThumbnailField)

    await wrapper.find('button').trigger('click')

    expect(click).toHaveBeenCalledTimes(1)
    expect(click.mock.contexts[0]).toBe(wrapper.find('input[type="file"]').element)
  })

  it('previews a picked image locally and emits it without uploading', async () => {
    const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:picked')
    const { wrapper } = await renderWithProviders(ThumbnailField)
    const file = image('nuevo.png')

    await pickFiles(wrapper, [file])

    expect(createObjectURL).toHaveBeenCalledWith(file)
    expect(wrapper.find('img').attributes('src')).toBe('blob:picked')
    expect(wrapper.find('.thumb__name').text()).toBe('nuevo.png')
    expect(buttonLabels(wrapper)).toEqual([
      t('entities.file.thumbnail.change'),
      t('entities.file.thumbnail.remove'),
    ])
    expect(wrapper.emitted('update:file')).toEqual([[file]])
    expect((wrapper.find('input[type="file"]').element as HTMLInputElement).value).toBe('')
  })

  it('ignores a change event without files', async () => {
    const { wrapper } = await renderWithProviders(ThumbnailField)

    await pickFiles(wrapper, [])

    expect(wrapper.emitted('update:file')).toBeUndefined()
    expect(wrapper.find('img').exists()).toBe(false)
  })

  it('rejects images over 10 MB and clears the error after a valid pick', async () => {
    const { wrapper } = await renderWithProviders(ThumbnailField)

    await pickFiles(wrapper, [image('huge.png', 10 * 1024 * 1024 + 1)])

    expect(wrapper.find('.thumb__error').text()).toBe(t('entities.file.thumbnail.tooLarge'))
    expect(wrapper.emitted('update:file')).toBeUndefined()
    expect(wrapper.find('img').exists()).toBe(false)

    const exact = image('limit.png', 10 * 1024 * 1024)
    await pickFiles(wrapper, [exact])

    expect(wrapper.find('.thumb__error').exists()).toBe(false)
    expect(wrapper.emitted('update:file')).toEqual([[exact]])
  })

  it('revokes the previous local preview when another image is picked and on unmount', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValueOnce('blob:one').mockReturnValueOnce('blob:two')
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const { wrapper } = await renderWithProviders(ThumbnailField)

    await pickFiles(wrapper, [image('one.png')])
    await pickFiles(wrapper, [image('two.png')])

    expect(revoke).toHaveBeenCalledWith('blob:one')
    expect(wrapper.find('img').attributes('src')).toBe('blob:two')

    wrapper.unmount()
    expect(revoke).toHaveBeenLastCalledWith('blob:two')
  })

  it('restores the stored thumbnail when the picked image is removed', async () => {
    const requested = serveFileNames({ 'file-1': { name: 'poster', extension: '.png' } })
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:picked')
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const { wrapper } = await renderWithProviders(ThumbnailField, {
      props: { existingThumbnailId: 'file-1' },
    })
    await flushPromises()

    await pickFiles(wrapper, [image('nuevo.png')])
    expect(wrapper.find('.thumb__name').text()).toBe('nuevo.png')

    const remove = wrapper.findAll('button')[1]
    await remove?.trigger('click')
    await flushPromises()

    expect(wrapper.emitted('update:file')?.at(-1)).toEqual([null])
    expect(revoke).toHaveBeenCalledWith('blob:picked')
    expect(wrapper.find('img').attributes('src')).toBe('/api/files/file-1/content')
    expect(wrapper.find('.thumb__name').text()).toBe('poster.png')
    expect(buttonLabels(wrapper)).toEqual([t('entities.file.thumbnail.change')])
    expect(requested).toEqual(['file-1', 'file-1'])
  })

  it('returns to the placeholder when the picked image is removed without a stored one', async () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const { wrapper } = await renderWithProviders(ThumbnailField)

    await pickFiles(wrapper, [image()])
    const remove = wrapper.findAll('button')[1]
    await remove?.trigger('click')

    expect(wrapper.emitted('update:file')?.at(-1)).toEqual([null])
    expect(revoke).toHaveBeenCalledWith('blob:test-object-url')
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.thumb__name').exists()).toBe(false)
    expect(buttonLabels(wrapper)).toEqual([t('entities.file.thumbnail.select')])
  })

  it('follows changes of the stored thumbnail id and drops a picked image', async () => {
    const requested = serveFileNames({
      'file-1': { name: 'uno', extension: '.png' },
      'file-2': { name: 'dos', extension: '.jpg' },
    })
    const { wrapper } = await renderWithProviders(ThumbnailField, {
      props: { existingThumbnailId: 'file-1' },
    })
    await flushPromises()

    await pickFiles(wrapper, [image('local.png')])
    await wrapper.setProps({ existingThumbnailId: 'file-2' })
    await flushPromises()

    expect(wrapper.find('img').attributes('src')).toBe('/api/files/file-2/content')
    expect(wrapper.find('.thumb__name').text()).toBe('dos.jpg')
    expect(buttonLabels(wrapper)).toEqual([t('entities.file.thumbnail.change')])

    await wrapper.setProps({ existingThumbnailId: null })

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.thumb__name').exists()).toBe(false)
    expect(requested).toEqual(['file-1', 'file-2'])
  })

  it('shows an empty name for a stored thumbnail that no longer exists', async () => {
    serveFileNames({})
    const { wrapper } = await renderWithProviders(ThumbnailField, {
      props: { existingThumbnailId: 'gone' },
    })
    await flushPromises()

    expect(wrapper.find('img').attributes('src')).toBe('/api/files/gone/content')
    expect(wrapper.find('.thumb__name').exists()).toBe(false)
  })
})
