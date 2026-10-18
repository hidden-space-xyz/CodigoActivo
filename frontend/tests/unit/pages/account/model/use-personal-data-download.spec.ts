import { afterEach, describe, expect, it, vi } from 'vitest'

import { usePersonalDataDownload } from '@/pages/account/model/use-personal-data-download'
import { todayIso } from '@/shared/lib/date'

import { mountComposable, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

const COPY = { account: { firstName: 'Ada' }, dependents: [] }

function saved() {
  const blobs: Blob[] = []
  const names: string[] = []
  vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
    blobs.push(blob as Blob)
    return 'blob:personal-data'
  })
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    names.push(this.download)
  })
  return { blobs, names }
}

function notificationTexts(): string[] {
  return [...document.body.querySelectorAll('.el-notification')].map((n) => n.textContent)
}

describe('usePersonalDataDownload', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('saves the copy as an indented JSON file named after today', async () => {
    let requests = 0
    server.use(
      http.get('/api/me/personal-data', () => {
        requests += 1
        return HttpResponse.json(COPY)
      }),
    )
    const files = saved()
    const { result } = await mountComposable(() => usePersonalDataDownload())

    result.download()
    result.download()
    await vi.waitFor(() => expect(files.names).toHaveLength(1))

    expect(requests).toBe(1)
    expect(files.names[0]).toBe(t('pages.account.personalData.fileName', { date: todayIso() }))
    expect(files.blobs[0]?.type).toBe('application/json')
    expect(await files.blobs[0]?.text()).toBe(JSON.stringify(COPY, null, 2))
    await vi.waitFor(() => expect(result.isDownloading.value).toBe(false))
  })

  it('reports a failed request and saves nothing', async () => {
    server.use(http.get('/api/me/personal-data', () => apiError(404, 'UserNotFound')))
    const files = saved()
    const { result } = await mountComposable(() => usePersonalDataDownload(), { attach: true })

    result.download()

    await vi.waitFor(() => expect(notificationTexts().join()).toContain(t('errors.UserNotFound')))
    expect(files.names).toEqual([])
  })
})
