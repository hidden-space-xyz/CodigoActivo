import { describe, expect, it } from 'vitest'

import { uploadFileRequest } from '@/entities/file'
import { getThumbnailNameRequest, uploadThumbnailRequest } from '@/entities/file/api/requests'
import { ApiError } from '@/shared/api'

import { readMultipartFile } from '../../../../support/fixtures/entities/multipart'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

interface Upload {
  readonly method: string
  readonly path: string
  readonly file: { name: string; content: string } | null
  readonly csrf: string | null
}

async function readUpload(request: Request): Promise<Upload> {
  return {
    method: request.method,
    path: new URL(request.url).pathname,
    csrf: request.headers.get('X-CSRF-TOKEN'),
    file: await readMultipartFile(request),
  }
}

function image(name = 'poster.png'): File {
  return new File(['image-bytes'], name, { type: 'image/png' })
}

describe('file requests', () => {
  describe('uploadThumbnailRequest', () => {
    it('creates a new file as multipart form data and returns its id', async () => {
      let upload: Upload | undefined
      server.use(
        http.post('/api/files', async ({ request }) => {
          upload = await readUpload(request)
          return HttpResponse.json({ id: 'file-new' })
        }),
      )

      await expect(uploadThumbnailRequest(image())).resolves.toBe('file-new')
      expect(upload).toEqual({
        method: 'POST',
        path: '/api/files',
        file: { name: 'poster.png', content: 'image-bytes' },
        csrf: TEST_CSRF_TOKEN,
      })
    })

    it('returns an empty id when the API omits it', async () => {
      server.use(http.post('/api/files', () => HttpResponse.json({})))

      await expect(uploadThumbnailRequest(image(), null)).resolves.toBe('')
    })

    it('replaces the existing file in place and keeps its id', async () => {
      let upload: Upload | undefined
      server.use(
        http.put('/api/files/file-1', async ({ request }) => {
          upload = await readUpload(request)
          return HttpResponse.json({ id: 'file-1' })
        }),
      )

      await expect(uploadThumbnailRequest(image('new.png'), 'file-1')).resolves.toBe('file-1')
      expect(upload).toEqual({
        method: 'PUT',
        path: '/api/files/file-1',
        file: { name: 'new.png', content: 'image-bytes' },
        csrf: TEST_CSRF_TOKEN,
      })
    })

    it('rejects with the API error when the upload fails', async () => {
      server.use(http.post('/api/files', () => apiError(413, 'FileUploadTooLarge')))

      await expect(uploadThumbnailRequest(image())).rejects.toMatchObject({
        status: 413,
        code: 'FileUploadTooLarge',
      })
    })
  })

  describe('getThumbnailNameRequest', () => {
    it('joins the name and extension', async () => {
      server.use(
        http.get('/api/files/file-1', () =>
          HttpResponse.json({ name: 'poster', extension: '.png' }),
        ),
      )

      await expect(getThumbnailNameRequest('file-1')).resolves.toBe('poster.png')
    })

    it('returns what is available when parts are missing', async () => {
      server.use(http.get('/api/files/file-2', () => HttpResponse.json({ name: 'poster' })))

      await expect(getThumbnailNameRequest('file-2')).resolves.toBe('poster')
    })

    it('returns an empty name when the file no longer exists', async () => {
      server.use(http.get('/api/files/gone', () => apiError(404, 'FileNotFound')))

      await expect(getThumbnailNameRequest('gone')).resolves.toBe('')
    })

    it('rethrows other errors', async () => {
      server.use(http.get('/api/files/broken', () => apiError(500)))

      await expect(getThumbnailNameRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  describe('uploadFileRequest', () => {
    it('uploads a new file and returns its id or undefined', async () => {
      const uploads: Upload[] = []
      let id: string | undefined = 'file-9'
      server.use(
        http.post('/api/files', async ({ request }) => {
          uploads.push(await readUpload(request))
          return HttpResponse.json(id ? { id } : {})
        }),
      )

      await expect(uploadFileRequest(image('doc.pdf'))).resolves.toBe('file-9')
      id = undefined
      await expect(uploadFileRequest(image('doc.pdf'))).resolves.toBeUndefined()
      expect(uploads.map((upload) => upload.file?.name)).toEqual(['doc.pdf', 'doc.pdf'])
    })
  })
})
