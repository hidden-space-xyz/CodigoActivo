import { ref } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useThumbnailUpload } from '@/entities/file'

import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { readMultipartFile } from '../../../../support/multipart'
import { mountComposable } from '../../../../support/render'

function image(): File {
  return new File(['bytes'], 'poster.png', { type: 'image/png' })
}

describe('useThumbnailUpload', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('reports a missing thumbnail until a file is picked or an id exists', async () => {
    const existingId = ref<string | null>(null)
    const { result } = await mountComposable(() => useThumbnailUpload(existingId))

    expect(result.missingThumbnail.value).toBe(true)

    result.pickedFile.value = image()
    expect(result.missingThumbnail.value).toBe(false)

    result.pickedFile.value = null
    existingId.value = 'file-1'
    expect(result.missingThumbnail.value).toBe(false)
  })

  it('returns the existing id, or null, without uploading when nothing was picked', async () => {
    const existingId = ref<string | null | undefined>('file-1')
    const { result } = await mountComposable(() => useThumbnailUpload(existingId))

    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')

    existingId.value = undefined
    await expect(result.resolveThumbnailId()).resolves.toBeNull()
    expect(result.uploading.value).toBe(false)
  })

  it('uploads a picked file as a new thumbnail', async () => {
    let fileName: string | undefined
    server.use(
      http.post('/api/files', async ({ request }) => {
        fileName = (await readMultipartFile(request))?.name
        return HttpResponse.json({ id: 'file-new' })
      }),
    )
    const { result } = await mountComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    const pending = result.resolveThumbnailId()
    expect(result.uploading.value).toBe(true)

    await expect(pending).resolves.toBe('file-new')
    expect(result.uploading.value).toBe(false)
    expect(result.uploadError.value).toBe('')
    expect(fileName).toBe('poster.png')
  })

  it('uploads a picked file as a new file even when one exists', async () => {
    server.use(http.post('/api/files', () => HttpResponse.json({ id: 'file-new' })))
    const { result } = await mountComposable(() => useThumbnailUpload(() => 'file-1'))
    result.pickedFile.value = image()

    await expect(result.resolveThumbnailId()).resolves.toBe('file-new')
  })

  it('uploads the same pick once and a new pick again', async () => {
    let uploads = 0
    server.use(
      http.post('/api/files', () => {
        uploads += 1
        return HttpResponse.json({ id: `file-${uploads}` })
      }),
    )
    const { result } = await mountComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')
    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')
    expect(uploads).toBe(1)

    result.pickedFile.value = image()
    await expect(result.resolveThumbnailId()).resolves.toBe('file-2')
    expect(uploads).toBe(2)
  })

  it('uploads the same pick again after a day or a reset', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-18T10:00:00Z'))
    let uploads = 0
    server.use(
      http.post('/api/files', () => {
        uploads += 1
        return HttpResponse.json({ id: `file-${uploads}` })
      }),
    )
    const { result } = await mountComposable(() => useThumbnailUpload(null))
    const pick = image()
    result.pickedFile.value = pick

    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')
    vi.setSystemTime(new Date('2026-10-19T09:59:00Z'))
    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')
    vi.setSystemTime(new Date('2026-10-19T10:00:00Z'))
    await expect(result.resolveThumbnailId()).resolves.toBe('file-2')

    result.reset()
    result.pickedFile.value = pick
    await expect(result.resolveThumbnailId()).resolves.toBe('file-3')
  })

  it('translates a known API error code and returns null on failure', async () => {
    server.use(http.post('/api/files', () => apiError(413, 'FileUploadTooLarge')))
    const { result } = await mountComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    await expect(result.resolveThumbnailId()).resolves.toBeNull()

    expect(result.uploadError.value).toBe(t('errors.FileUploadTooLarge'))
    expect(result.uploading.value).toBe(false)
  })

  it('falls back to the generic upload message and clears it on retry and reset', async () => {
    let fail = true
    server.use(
      http.post('/api/files', () => (fail ? apiError(500) : HttpResponse.json({ id: 'file-2' }))),
    )
    const { result } = await mountComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    await result.resolveThumbnailId()
    expect(result.uploadError.value).toBe(t('entities.file.thumbnail.uploadFailed'))

    fail = false
    await expect(result.resolveThumbnailId()).resolves.toBe('file-2')
    expect(result.uploadError.value).toBe('')

    fail = true
    result.pickedFile.value = image()
    await result.resolveThumbnailId()
    expect(result.uploadError.value).toBe(t('entities.file.thumbnail.uploadFailed'))
    result.reset()
    expect(result.uploadError.value).toBe('')
    expect(result.pickedFile.value).toBeNull()
  })
})
