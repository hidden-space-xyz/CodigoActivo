import { ref } from 'vue'
import { describe, expect, it } from 'vitest'

import { useThumbnailUpload } from '@/entities/file'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { readMultipartFile } from '../../../../support/fixtures/entities/multipart'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

function image(): File {
  return new File(['bytes'], 'poster.png', { type: 'image/png' })
}

describe('useThumbnailUpload', () => {
  it('reports a missing thumbnail until a file is picked or an id exists', async () => {
    const existingId = ref<string | null>(null)
    const { result } = await renderComposable(() => useThumbnailUpload(existingId))

    expect(result.missingThumbnail.value).toBe(true)

    result.pickedFile.value = image()
    expect(result.missingThumbnail.value).toBe(false)

    result.pickedFile.value = null
    existingId.value = 'file-1'
    expect(result.missingThumbnail.value).toBe(false)
  })

  it('returns the existing id, or null, without uploading when nothing was picked', async () => {
    const existingId = ref<string | null | undefined>('file-1')
    const { result } = await renderComposable(() => useThumbnailUpload(existingId))

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
    const { result } = await renderComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    const pending = result.resolveThumbnailId()
    expect(result.uploading.value).toBe(true)

    await expect(pending).resolves.toBe('file-new')
    expect(result.uploading.value).toBe(false)
    expect(result.uploadError.value).toBe('')
    expect(fileName).toBe('poster.png')
  })

  it('overwrites the existing file when one is set', async () => {
    let replaced = false
    server.use(
      http.put('/api/files/file-1', () => {
        replaced = true
        return HttpResponse.json({ id: 'file-1' })
      }),
    )
    const { result } = await renderComposable(() => useThumbnailUpload(() => 'file-1'))
    result.pickedFile.value = image()

    await expect(result.resolveThumbnailId()).resolves.toBe('file-1')
    expect(replaced).toBe(true)
  })

  it('translates a known API error code and returns null on failure', async () => {
    server.use(http.post('/api/files', () => apiError(413, 'FileUploadTooLarge')))
    const { result } = await renderComposable(() => useThumbnailUpload(null))
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
    const { result } = await renderComposable(() => useThumbnailUpload(null))
    result.pickedFile.value = image()

    await result.resolveThumbnailId()
    expect(result.uploadError.value).toBe(t('entities.file.thumbnail.uploadFailed'))

    fail = false
    await expect(result.resolveThumbnailId()).resolves.toBe('file-2')
    expect(result.uploadError.value).toBe('')

    fail = true
    await result.resolveThumbnailId()
    result.reset()
    expect(result.uploadError.value).toBe('')
    expect(result.pickedFile.value).toBeNull()
  })
})
