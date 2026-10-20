import { describe, expect, it } from 'vitest'

import { uploadFileRequest } from '@/entities/file'
import { getThumbnailNameRequest } from '@/entities/file/api/requests'
import { ApiError } from '@/shared/api'

import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'
import { readMultipartFile } from '../../../../support/multipart'

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
  describe('uploadFileRequest', () => {
    it('creates a new file as multipart form data and returns its id', async () => {
      let upload: Upload | undefined
      server.use(
        http.post('/api/files', async ({ request }) => {
          upload = await readUpload(request)
          return HttpResponse.json({ id: 'file-new' })
        }),
      )

      await expect(uploadFileRequest(image())).resolves.toBe('file-new')
      expect(upload).toEqual({
        method: 'POST',
        path: '/api/files',
        file: { name: 'poster.png', content: 'image-bytes' },
        csrf: TEST_CSRF_TOKEN,
      })
    })

    it('rejects with the API error when the upload fails', async () => {
      server.use(http.post('/api/files', () => apiError(413, 'FileUploadTooLarge')))

      await expect(uploadFileRequest(image())).rejects.toMatchObject({
        status: 413,
        code: 'FileUploadTooLarge',
      })
    })
  })

  describe('getThumbnailNameRequest', () => {
    it('returns the stored name, which already carries the extension', async () => {
      server.use(
        http.get('/api/files/file-1', () =>
          HttpResponse.json({ name: 'poster.png', extension: 'png' }),
        ),
      )

      await expect(getThumbnailNameRequest('file-1')).resolves.toBe('poster.png')
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
})
