import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  certificateKey,
  useAccountCertificates,
} from '@/features/account/model/useAccountCertificates'
import {
  downloadCertificatePdf,
  downloadCertificatePng,
} from '@/features/account/model/certificate-sheet'

import {
  buildCertificate,
  buildCertificateResponse,
} from '../../../../support/fixtures/account/account'
import { http, HttpResponse, server } from '../../../../support/server'
import { withSetup } from './with-setup'

vi.mock('@/features/account/model/certificate-sheet', () => ({
  downloadCertificatePdf: vi.fn(),
  downloadCertificatePng: vi.fn(),
}))

beforeEach(() => {
  vi.mocked(downloadCertificatePdf).mockReset()
  vi.mocked(downloadCertificatePng).mockReset()
})

function deferred() {
  let resolve: () => void = () => undefined
  let reject: (error: unknown) => void = () => undefined
  const promise = new Promise<void>((onResolve, onReject) => {
    resolve = onResolve
    reject = onReject
  })
  return { promise, resolve, reject }
}

describe('certificateKey', () => {
  it('combines the event and participant into a stable identity', () => {
    expect(certificateKey(buildCertificate({ eventId: 'e-9', participantId: 'child-2' }))).toBe(
      'e-9-child-2',
    )
  })
})

describe('useAccountCertificates', () => {
  it('does not request certificates without a signed-in user', async () => {
    const requested = vi.fn()
    server.use(
      http.get('/api/me/certificates', () => {
        requested()
        return HttpResponse.json([])
      }),
    )

    const { result } = await withSetup(() => useAccountCertificates())

    expect(requested).not.toHaveBeenCalled()
    expect(result.entries.value).toEqual([])
  })

  it('loads and maps the certificates of the signed-in user', async () => {
    server.use(
      http.get('/api/me/certificates', () =>
        HttpResponse.json([buildCertificateResponse(), { eventId: 'event-2', userId: 'child-1' }]),
      ),
    )

    const { result } = await withSetup(() => useAccountCertificates(), { user: {} })
    await vi.waitFor(() => expect(result.entries.value).toHaveLength(2))

    expect(result.entries.value[0]).toEqual(buildCertificate())
    expect(result.entries.value[1]).toEqual({
      code: '',
      eventId: 'event-2',
      participantId: 'child-1',
      firstName: '',
      lastName: '',
      isSelf: false,
      eventTitle: '',
      eventSubtitle: '',
      startsAt: '',
      endsAt: '',
    })
  })

  it('opens and closes the preview', async () => {
    server.use(http.get('/api/me/certificates', () => HttpResponse.json([])))
    const { result } = await withSetup(() => useAccountCertificates(), { user: {} })
    const certificate = buildCertificate()

    result.open(certificate)
    expect(result.preview.value).toEqual(certificate)

    result.close()
    expect(result.preview.value).toBeNull()
  })

  it('downloads in the requested format', async () => {
    server.use(http.get('/api/me/certificates', () => HttpResponse.json([])))
    const { result } = await withSetup(() => useAccountCertificates(), { user: {} })
    const certificate = buildCertificate()

    await result.download(certificate, 'pdf')
    await result.download(certificate, 'png')

    expect(downloadCertificatePdf).toHaveBeenCalledExactlyOnceWith(certificate)
    expect(downloadCertificatePng).toHaveBeenCalledExactlyOnceWith(certificate)
  })

  it('marks a download busy per format and ignores repeated clicks until it finishes', async () => {
    server.use(http.get('/api/me/certificates', () => HttpResponse.json([])))
    const { result } = await withSetup(() => useAccountCertificates(), { user: {} })
    const certificate = buildCertificate()
    const pending = deferred()
    vi.mocked(downloadCertificatePng).mockReturnValueOnce(pending.promise)

    const first = result.download(certificate, 'png')
    const repeated = result.download(certificate, 'png')

    expect(result.isBusy(certificate, 'png')).toBe(true)
    expect(result.isBusy(certificate, 'pdf')).toBe(false)
    expect(result.isBusy(buildCertificate({ participantId: 'child-1' }), 'png')).toBe(false)
    expect(result.isBusy(null, 'png')).toBe(false)

    await repeated
    expect(downloadCertificatePng).toHaveBeenCalledTimes(1)

    pending.resolve()
    await first
    expect(result.isBusy(certificate, 'png')).toBe(false)
  })

  it('clears the busy state and propagates the error when a download fails', async () => {
    server.use(http.get('/api/me/certificates', () => HttpResponse.json([])))
    const { result } = await withSetup(() => useAccountCertificates(), { user: {} })
    const certificate = buildCertificate()
    const failure = new Error('render failed')
    vi.mocked(downloadCertificatePdf).mockRejectedValueOnce(failure)

    await expect(result.download(certificate, 'pdf')).rejects.toBe(failure)
    await flushPromises()

    expect(result.isBusy(certificate, 'pdf')).toBe(false)
  })
})
