import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  downloadCertificatePdf,
  downloadCertificatePng,
  renderCertificatePreview,
} from '@/pages/account/lib/certificate-sheet'
import CertificatesSection from '@/pages/account/ui/CertificatesSection.vue'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildCertificateResponse } from '../../../../support/builders'
import {
  click,
  findButton,
  findButtons,
  notificationTexts,
  openDialogs,
} from '../../../../support/dom'
import { buildCertificate } from '../../../../support/models'
import { formatDateRange } from '@/shared/lib/date'

vi.mock('@/pages/account/lib/certificate-sheet', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/pages/account/lib/certificate-sheet')>()),
  renderCertificatePreview: vi.fn(),
  downloadCertificatePng: vi.fn(),
  downloadCertificatePdf: vi.fn(),
}))

const CHILD_CERTIFICATE = buildCertificateResponse({
  code: 'CA-2025-0002',
  userId: 'child-1',
  firstName: 'Byron',
  lastName: 'Lovelace',
  isSelf: false,
})

function serveCertificates(body = [buildCertificateResponse(), CHILD_CERTIFICATE]) {
  server.use(http.get('/api/me/certificates', () => HttpResponse.json(body)))
}

async function renderSection() {
  const { wrapper } = await renderWithProviders(CertificatesSection, { user: {}, attach: true })
  await flushPromises()
  return wrapper
}

function deferred() {
  let resolve: () => void = () => undefined
  const promise = new Promise<void>((onResolve) => {
    resolve = onResolve
  })
  return { promise, resolve }
}

beforeEach(() => {
  vi.mocked(renderCertificatePreview).mockReset().mockResolvedValue()
  vi.mocked(downloadCertificatePng).mockReset().mockResolvedValue()
  vi.mocked(downloadCertificatePdf).mockReset().mockResolvedValue()
})

describe('CertificatesSection', () => {
  it('shows a loading state while certificates are requested', async () => {
    const pending = deferred()
    server.use(
      http.get('/api/me/certificates', async () => {
        await pending.promise
        return HttpResponse.json([])
      }),
    )

    const wrapper = await renderSection()

    expect(wrapper.text()).toContain(t('common.loading'))
    pending.resolve()
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.certificates.empty')))
  })

  it('explains when certificates cannot be loaded', async () => {
    server.use(http.get('/api/me/certificates', () => apiError(500)))

    const wrapper = await renderSection()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.certificates.error')))
  })

  it('lists a tile per certificate with participant, event, dates and code', async () => {
    serveCertificates()

    const wrapper = await renderSection()

    const tiles = wrapper.findAll('.cert-tile')
    expect(tiles).toHaveLength(2)
    const [adaTile, byronTile] = tiles
    expect(adaTile?.text()).toContain('Ada Lovelace')
    expect(adaTile?.text()).toContain('Hackathon de Primavera')
    expect(adaTile?.text()).toContain(formatDateRange('2025-05-10', '2025-05-12'))
    expect(adaTile?.text()).toContain('CA-2025-0001')
    expect(byronTile?.text()).toContain('Byron Lovelace')
    expect(adaTile?.find('.cert-tile__open').attributes('aria-label')).toBe(
      t('pages.account.certificates.openAria', {
        name: 'Ada Lovelace',
        event: 'Hackathon de Primavera',
      }),
    )
  })

  it('opens the preview from the tile and from the view button, and closes it', async () => {
    serveCertificates()
    const wrapper = await renderSection()

    await wrapper.findAll('.cert-tile__open')[1]?.trigger('click')
    await flushPromises()

    expect(openDialogs()).toHaveLength(1)
    expect(renderCertificatePreview).toHaveBeenLastCalledWith(
      expect.any(HTMLCanvasElement),
      buildCertificate({
        code: 'CA-2025-0002',
        participantId: 'child-1',
        firstName: 'Byron',
        isSelf: false,
      }),
      expect.any(Function),
    )

    await click(findButton(t('common.close'), document.body))
    expect(openDialogs()).toHaveLength(0)

    await click(findButtons(t('pages.account.certificates.view'), document.body)[0] as Element)
    expect(openDialogs()).toHaveLength(1)
    expect(renderCertificatePreview).toHaveBeenLastCalledWith(
      expect.any(HTMLCanvasElement),
      buildCertificate(),
      expect.any(Function),
    )
  })

  it('downloads a PNG and a PDF from the tile, showing progress meanwhile', async () => {
    serveCertificates([buildCertificateResponse()])
    const pending = deferred()
    vi.mocked(downloadCertificatePng).mockReturnValueOnce(pending.promise)
    const wrapper = await renderSection()
    const tile = wrapper.find('.cert-tile').element

    await click(findButton(t('pages.account.certificates.png'), tile))

    expect(downloadCertificatePng).toHaveBeenCalledExactlyOnceWith(
      buildCertificate(),
      expect.any(Function),
    )
    const png = findButton(t('pages.account.certificates.png'), tile)
    expect(png.getAttribute('aria-busy')).toBe('true')
    expect(
      findButton(t('pages.account.certificates.pdf'), tile).getAttribute('aria-busy'),
    ).toBeNull()

    pending.resolve()
    await flushPromises()
    expect(png.getAttribute('aria-busy')).toBeNull()

    await click(findButton(t('pages.account.certificates.pdf'), tile))
    expect(downloadCertificatePdf).toHaveBeenCalledExactlyOnceWith(
      buildCertificate(),
      expect.any(Function),
    )
    expect(notificationTexts()).toHaveLength(0)
  })

  it('downloads from the preview dialog and reflects progress there', async () => {
    serveCertificates([buildCertificateResponse()])
    const pending = deferred()
    vi.mocked(downloadCertificatePdf).mockReturnValueOnce(pending.promise)
    const wrapper = await renderSection()

    await wrapper.find('.cert-tile__open').trigger('click')
    await flushPromises()
    const [dialog] = openDialogs()
    if (!dialog) throw new Error('Preview did not open')
    const footer = dialog.querySelector('.cert-preview__actions') ?? dialog

    await click(findButton(t('pages.account.certificates.downloadPdf'), footer))
    expect(downloadCertificatePdf).toHaveBeenCalledExactlyOnceWith(
      buildCertificate(),
      expect.any(Function),
    )
    expect(
      findButton(t('pages.account.certificates.downloadPdf'), footer).getAttribute('aria-busy'),
    ).toBe('true')

    await click(findButton(t('pages.account.certificates.download'), footer))
    expect(downloadCertificatePng).toHaveBeenCalledExactlyOnceWith(
      buildCertificate(),
      expect.any(Function),
    )

    pending.resolve()
    await flushPromises()
  })

  it('notifies the user when a certificate cannot be generated', async () => {
    serveCertificates([buildCertificateResponse()])
    vi.mocked(downloadCertificatePng).mockRejectedValueOnce(new Error('canvas unavailable'))
    const wrapper = await renderSection()

    await click(findButton(t('pages.account.certificates.png'), wrapper.find('.cert-tile').element))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('common.error'))
    expect(notificationTexts()[0]).toContain(t('errors.generic'))
  })
})
