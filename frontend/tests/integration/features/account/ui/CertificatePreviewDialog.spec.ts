import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { renderCertificatePreview } from '@/features/account/model/certificate-sheet'
import CertificatePreviewDialog from '@/features/account/ui/CertificatePreviewDialog.vue'

import { buildCertificate } from '../../../../support/fixtures/account/account'
import { buttonByText, click, openDialogs } from '../../../../support/fixtures/account/dom'
import { renderWithProviders, t } from '../../../../support/render'

vi.mock('@/features/account/model/certificate-sheet', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/account/model/certificate-sheet')>()),
  renderCertificatePreview: vi.fn(),
}))

function deferred() {
  let resolve: () => void = () => undefined
  let reject: (error: unknown) => void = () => undefined
  const promise = new Promise<void>((onResolve, onReject) => {
    resolve = onResolve
    reject = onReject
  })
  return { promise, resolve, reject }
}

async function renderDialog(props: Record<string, unknown> = {}) {
  const { wrapper } = await renderWithProviders(CertificatePreviewDialog, {
    props: { certificate: buildCertificate(), busyPng: false, busyPdf: false, ...props },
  })
  return wrapper
}

function veilText(): string | undefined {
  return document.querySelector('.cert-preview__veil')?.textContent.trim()
}

beforeEach(() => {
  vi.mocked(renderCertificatePreview).mockReset()
})

describe('CertificatePreviewDialog', () => {
  it('stays closed and paints nothing without a certificate', async () => {
    await renderDialog({ certificate: null })

    expect(openDialogs()).toHaveLength(0)
    expect(renderCertificatePreview).not.toHaveBeenCalled()
  })

  it('paints the certificate on the canvas and shows a loading veil meanwhile', async () => {
    const pending = deferred()
    vi.mocked(renderCertificatePreview).mockReturnValueOnce(pending.promise)
    const certificate = buildCertificate()

    await renderDialog({ certificate })

    const [dialog] = openDialogs()
    expect(dialog?.textContent).toContain(t('features.account.certificates.dialog.header'))
    expect(dialog?.textContent).toContain(t('features.account.certificates.dialog.hint'))
    const canvas = document.querySelector('canvas')
    expect(canvas?.getAttribute('aria-label')).toBe(certificate.eventTitle)
    expect(renderCertificatePreview).toHaveBeenCalledExactlyOnceWith(canvas, certificate)
    expect(veilText()).toBe(t('common.loading'))

    pending.resolve()
    await flushPromises()

    expect(veilText()).toBeUndefined()
  })

  it('shows an error veil when the certificate cannot be painted', async () => {
    vi.mocked(renderCertificatePreview).mockRejectedValueOnce(new Error('no canvas'))

    await renderDialog()
    await flushPromises()

    expect(veilText()).toBe(t('features.account.certificates.renderError'))
  })

  it('ignores the outcome of a superseded paint when another certificate is opened', async () => {
    const first = deferred()
    const second = deferred()
    vi.mocked(renderCertificatePreview)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise)

    const wrapper = await renderDialog()
    await wrapper.setProps({ certificate: buildCertificate({ eventId: 'event-2' }) })
    await flushPromises()
    expect(renderCertificatePreview).toHaveBeenCalledTimes(2)

    first.reject(new Error('stale failure'))
    await flushPromises()
    expect(veilText()).toBe(t('common.loading'))

    second.resolve()
    await flushPromises()
    expect(veilText()).toBeUndefined()
  })

  it('emits downloads for the previewed certificate in each format', async () => {
    vi.mocked(renderCertificatePreview).mockResolvedValue()
    const certificate = buildCertificate()
    const wrapper = await renderDialog({ certificate })

    await click(buttonByText(document.body, t('features.account.certificates.downloadPdf')))
    await click(buttonByText(document.body, t('features.account.certificates.download')))

    expect(wrapper.emitted('download')).toEqual([
      [certificate, 'pdf'],
      [certificate, 'png'],
    ])
  })

  it('shows busy download buttons while files are generated', async () => {
    vi.mocked(renderCertificatePreview).mockResolvedValue()
    await renderDialog({ busyPng: true, busyPdf: true })

    const pdf = buttonByText(document.body, t('features.account.certificates.downloadPdf'))
    const png = buttonByText(document.body, t('features.account.certificates.download'))
    expect(pdf.getAttribute('aria-busy')).toBe('true')
    expect(png.getAttribute('aria-busy')).toBe('true')
  })

  it('does not emit downloads once the certificate is gone', async () => {
    vi.mocked(renderCertificatePreview).mockResolvedValue()
    const wrapper = await renderDialog()
    const pdf = buttonByText(document.body, t('features.account.certificates.downloadPdf'))
    const png = buttonByText(document.body, t('features.account.certificates.download'))

    await wrapper.setProps({ certificate: null })
    await click(pdf)
    await click(png)

    expect(wrapper.emitted('download')).toBeUndefined()
  })

  it('emits close from the close button and when the dialog is dismissed', async () => {
    vi.mocked(renderCertificatePreview).mockResolvedValue()
    const wrapper = await renderDialog()

    await click(buttonByText(document.body, t('common.close')))
    const dialog = wrapper.findComponent(ElDialog)
    dialog.vm.$emit('update:modelValue', true)
    dialog.vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('close')).toHaveLength(2)
  })
})
