import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  certificateKey,
  useAccountCertificates,
} from '@/pages/account/model/use-account-certificates'
import {
  downloadCertificatePdf,
  downloadCertificatePng,
} from '@/pages/account/lib/certificate-sheet'

import { http, HttpResponse, server } from '../../../../support/server'
import { buildCertificate } from '../../../../support/models'
import { mountComposable } from '../../../../support/render'

vi.mock('@/pages/account/lib/certificate-sheet', () => ({
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

    const { result } = await mountComposable(() => useAccountCertificates())

    expect(requested).not.toHaveBeenCalled()
    expect(result.entries.value).toEqual([])
  })

  it('marks a download busy per format and ignores repeated clicks until it finishes', async () => {
    server.use(http.get('/api/me/certificates', () => HttpResponse.json([])))
    const { result } = await mountComposable(() => useAccountCertificates(), { user: {} })
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
    const { result } = await mountComposable(() => useAccountCertificates(), { user: {} })
    const certificate = buildCertificate()
    const failure = new Error('render failed')
    vi.mocked(downloadCertificatePdf).mockRejectedValueOnce(failure)

    await expect(result.download(certificate, 'pdf')).rejects.toBe(failure)
    await flushPromises()

    expect(result.isBusy(certificate, 'pdf')).toBe(false)
  })
})
