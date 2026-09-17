import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  downloadCertificatePdf,
  downloadCertificatePng,
  renderCertificatePreview,
} from '@/features/account/model/certificate-sheet'
import CertificatesSection from '@/features/account/ui/CertificatesSection.vue'
import { formatDateRange } from '@/shared/lib'

import {
  buildCertificate,
  buildCertificateResponse,
} from '../../../../support/fixtures/account/account'
import {
  buttonByText,
  buttonsByText,
  click,
  notificationTexts,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

vi.mock('@/features/account/model/certificate-sheet', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/account/model/certificate-sheet')>()),
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
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('features.account.certificates.empty')),
    )
  })

  it('explains when certificates cannot be loaded', async () => {
    server.use(http.get('/api/me/certificates', () => apiError(500)))

    const wrapper = await renderSection()

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('features.account.certificates.error')),
    )
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
      t('features.account.certificates.openAria', {
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
    )

    await click(buttonByText(document.body, t('common.close')))
    expect(openDialogs()).toHaveLength(0)

    await click(buttonsByText(document.body, t('features.account.certificates.view'))[0] as Element)
    expect(openDialogs()).toHaveLength(1)
    expect(renderCertificatePreview).toHaveBeenLastCalledWith(
      expect.any(HTMLCanvasElement),
      buildCertificate(),
    )
  })

  it('downloads a PNG and a PDF from the tile, showing progress meanwhile', async () => {
    serveCertificates([buildCertificateResponse()])
    const pending = deferred()
    vi.mocked(downloadCertificatePng).mockReturnValueOnce(pending.promise)
    const wrapper = await renderSection()
    const tile = wrapper.find('.cert-tile').element

    await click(buttonByText(tile, t('features.account.certificates.png')))

    expect(downloadCertificatePng).toHaveBeenCalledExactlyOnceWith(buildCertificate())
    const png = buttonByText(tile, t('features.account.certificates.png'))
    expect(png.getAttribute('aria-busy')).toBe('true')
    expect(
      buttonByText(tile, t('features.account.certificates.pdf')).getAttribute('aria-busy'),
    ).toBeNull()

    pending.resolve()
    await flushPromises()
    expect(png.getAttribute('aria-busy')).toBeNull()

    await click(buttonByText(tile, t('features.account.certificates.pdf')))
    expect(downloadCertificatePdf).toHaveBeenCalledExactlyOnceWith(buildCertificate())
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

    await click(buttonByText(footer, t('features.account.certificates.downloadPdf')))
    expect(downloadCertificatePdf).toHaveBeenCalledExactlyOnceWith(buildCertificate())
    expect(
      buttonByText(footer, t('features.account.certificates.downloadPdf')).getAttribute(
        'aria-busy',
      ),
    ).toBe('true')

    await click(buttonByText(footer, t('features.account.certificates.download')))
    expect(downloadCertificatePng).toHaveBeenCalledExactlyOnceWith(buildCertificate())

    pending.resolve()
    await flushPromises()
  })

  it('notifies the user when a certificate cannot be generated', async () => {
    serveCertificates([buildCertificateResponse()])
    vi.mocked(downloadCertificatePng).mockRejectedValueOnce(new Error('canvas unavailable'))
    const wrapper = await renderSection()

    await click(
      buttonByText(wrapper.find('.cert-tile').element, t('features.account.certificates.png')),
    )

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('common.error'))
    expect(notificationTexts()[0]).toContain(t('errors.generic'))
  })
})
