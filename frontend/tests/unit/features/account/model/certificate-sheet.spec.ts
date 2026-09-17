import { inflateSync } from 'node:zlib'

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { AccountCertificate } from '@/entities/account'

import { buildCertificate } from '../../../../support/fixtures/account/account'
import { createFakeContext, type TextDraw } from '../../../../support/fixtures/account/canvas'
import { t } from '../../../../support/render'

type Sheet = typeof import('@/features/account/model/certificate-sheet')

const PT_TO_MM = 0.352778
const RANGE_FORMATTER = new Intl.DateTimeFormat('es-ES', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})

let imageOutcome: 'load' | 'error' = 'load'
const createdImages: EventTarget[] = []

class FakeImage extends EventTarget {
  private source = ''

  get src(): string {
    return this.source
  }

  set src(value: string) {
    this.source = value
    createdImages.push(this)
    const outcome = imageOutcome
    queueMicrotask(() => this.dispatchEvent(new Event(outcome)))
  }
}

function stubFonts(load: (specimen: string) => Promise<unknown> = () => Promise.resolve([])) {
  const spy = vi.fn(load)
  Object.defineProperty(document, 'fonts', {
    configurable: true,
    value: { load: spy, ready: Promise.resolve() },
  })
  return spy
}

/** Loads a fresh copy of the module so its logo and font caches start empty. */
async function loadSheet(): Promise<Sheet> {
  vi.resetModules()
  return import('@/features/account/model/certificate-sheet')
}

function useFakeContext(fake = createFakeContext()) {
  const canvases: { canvas: HTMLCanvasElement; width: number; height: number }[] = []
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(function (
    this: HTMLCanvasElement,
  ) {
    canvases.push({ canvas: this, width: this.width, height: this.height })
    return fake.context
  })
  return { fake, canvases }
}

function textAt(texts: readonly TextDraw[], y: number): string {
  return texts
    .filter((draw) => draw.y === y)
    .map((draw) => draw.text)
    .join('')
}

function nameDraws(texts: readonly TextDraw[]): TextDraw[] {
  return texts.filter((draw) => draw.font.startsWith('700') && draw.font.includes('Space Grotesk'))
}

function titleDraws(texts: readonly TextDraw[]): TextDraw[] {
  return texts.filter((draw) => draw.font.startsWith('600') && draw.font.includes('Space Grotesk'))
}

async function renderPreview(sheet: Sheet, certificate: AccountCertificate = buildCertificate()) {
  const canvas = document.createElement('canvas')
  await sheet.renderCertificatePreview(canvas, certificate)
  return canvas
}

function captureDownloads() {
  const downloads: { name: string; blob: Blob }[] = []
  let pending: Blob | null = null
  vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
    pending = blob as Blob
    return 'blob:test-object-url'
  })
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    if (pending) downloads.push({ name: this.download, blob: pending })
  })
  return downloads
}

function stubToBlob(result: Blob | null) {
  return vi
    .spyOn(HTMLCanvasElement.prototype, 'toBlob')
    .mockImplementation((callback: BlobCallback) => callback(result))
}

beforeEach(() => {
  imageOutcome = 'load'
  createdImages.length = 0
  vi.stubGlobal('Image', FakeImage)
  stubFonts()
})

afterEach(() => {
  Reflect.deleteProperty(document, 'fonts')
})

describe('certificate sheet', () => {
  it('exposes the landscape A4 aspect ratio', async () => {
    const sheet = await loadSheet()

    expect(sheet.SHEET_RATIO).toBeCloseTo(297 / 210)
  })
})

describe('renderCertificatePreview', () => {
  it('sizes the canvas at preview resolution and scales drawing to millimetres', async () => {
    vi.stubGlobal('devicePixelRatio', 1)
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    const canvas = await renderPreview(sheet)

    expect(canvas.width).toBe(1069)
    expect(canvas.height).toBe(756)
    expect(fake.scales).toEqual([[1069 / 297, 756 / 210]])
    expect(fake.context.lineJoin).toBe('round')
    expect(fake.depth).toBe(0)
  })

  it('caps the device pixel ratio at two', async () => {
    vi.stubGlobal('devicePixelRatio', 3)
    const sheet = await loadSheet()
    useFakeContext()

    const canvas = await renderPreview(sheet)

    expect(canvas.width).toBe(2138)
    expect(canvas.height).toBe(1512)
  })

  it('falls back to a pixel ratio of one when the device reports none', async () => {
    vi.stubGlobal('devicePixelRatio', 0)
    const sheet = await loadSheet()
    useFakeContext()

    const canvas = await renderPreview(sheet)

    expect(canvas.width).toBe(1069)
  })

  it('waits for every brand font weight before painting', async () => {
    const load = stubFonts()
    const sheet = await loadSheet()
    useFakeContext()

    await renderPreview(sheet)

    expect(load).toHaveBeenCalledTimes(7)
    const specimens = load.mock.calls.map(([specimen]) => specimen)
    expect(specimens.some((specimen) => specimen.includes('Space Grotesk'))).toBe(true)
    expect(specimens.some((specimen) => specimen.includes('Hanken Grotesk'))).toBe(true)
    expect(specimens.some((specimen) => specimen.includes('JetBrains Mono'))).toBe(true)
  })

  it('draws the participant, event, dates, reference code and sheet wording', async () => {
    vi.stubGlobal('devicePixelRatio', 1)
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet)

    const drawn = fake.texts.map((draw) => draw.text)
    expect(nameDraws(fake.texts).map((draw) => draw.text)).toEqual(['Ada Lovelace'])
    expect(nameDraws(fake.texts)[0]?.font).toBe(
      `700 ${(32 * PT_TO_MM).toFixed(4)}px ${"'Space Grotesk', system-ui, sans-serif"}`,
    )
    expect(titleDraws(fake.texts).map((draw) => draw.text)).toEqual(['Hackathon de Primavera'])
    expect(drawn).toContain('CA-2025-0001')
    expect(drawn).toContain(t('features.account.certificates.sheet.preamble'))
    expect(drawn).toContain(t('features.account.certificates.sheet.connector'))
    expect(drawn).toContain(t('features.account.certificates.sheet.issuerValue'))
    expect(drawn).toContain('</>')

    const expectedDate = RANGE_FORMATTER.formatRange(new Date(2025, 4, 10), new Date(2025, 4, 12))
    expect(textAt(fake.texts, 144)).toBe(expectedDate.toLocaleUpperCase('es-ES'))
    expect(textAt(fake.texts, 60)).toBe(
      `// ${t('features.account.certificates.sheet.eyebrow')}`.toLocaleUpperCase('es-ES'),
    )
    expect(textAt(fake.texts, 166)).toBe(
      [
        t('features.account.certificates.sheet.issuerLabel'),
        t('features.account.certificates.sheet.registryLabel'),
      ]
        .join('')
        .toLocaleUpperCase('es-ES'),
    )
    expect(fake.depth).toBe(0)
  })

  it('draws the brand logo once it has loaded', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet)

    expect(createdImages).toHaveLength(1)
    expect(fake.images).toEqual([{ image: createdImages[0], args: [135.5, 24, 26, 26] }])
  })

  it('reuses the loaded logo and fonts on later renders', async () => {
    const load = stubFonts()
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet)
    await renderPreview(sheet)

    expect(createdImages).toHaveLength(1)
    expect(load).toHaveBeenCalledTimes(7)
    expect(fake.images).toHaveLength(2)
  })

  it('paints without the logo when it fails to load and retries on the next render', async () => {
    imageOutcome = 'error'
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet)
    expect(fake.images).toHaveLength(0)

    imageOutcome = 'load'
    await renderPreview(sheet)

    expect(createdImages).toHaveLength(2)
    expect(fake.images).toHaveLength(1)
  })

  it('rejects and retries font loading when the fonts fail to load', async () => {
    const failure = new Error('font network error')
    let attempts = 0
    const load = stubFonts(() => {
      attempts += 1
      return attempts === 1 ? Promise.reject(failure) : Promise.resolve([])
    })
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await expect(renderPreview(sheet)).rejects.toBe(failure)
    expect(fake.texts).toHaveLength(0)

    await renderPreview(sheet)

    expect(load.mock.calls.length).toBeGreaterThan(7)
    expect(fake.texts.length).toBeGreaterThan(0)
  })

  it('rejects with a localized error when the canvas has no 2D context', async () => {
    const sheet = await loadSheet()

    await expect(renderPreview(sheet)).rejects.toThrow(
      t('features.account.certificates.renderError'),
    )
  })

  it('shrinks and ellipsizes names too long for the sheet', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()
    const firstName = 'Maximiliana Wilhelmina Konstantina Bartholomea'
    const lastName = 'de la Santísima Trinidad Fernández-Villaverde'

    await renderPreview(sheet, buildCertificate({ firstName, lastName }))

    const [name] = nameDraws(fake.texts)
    expect(name?.font).toContain(`${(16 * PT_TO_MM).toFixed(4)}px`)
    expect(name?.text.endsWith('…')).toBe(true)
    expect(`${firstName} ${lastName}`.startsWith(name?.text.slice(0, -1) ?? '?')).toBe(true)
  })

  it('shows a dash when the participant has no name', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet, buildCertificate({ firstName: '', lastName: '' }))

    expect(nameDraws(fake.texts).map((draw) => draw.text)).toEqual(['—'])
  })

  it('wraps long event titles over two lines and lowers the date', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()
    const eventTitle = 'Jornadas de introducción a la programación creativa para toda la familia'

    await renderPreview(sheet, buildCertificate({ eventTitle }))

    const lines = titleDraws(fake.texts).map((draw) => draw.text)
    expect(lines).toHaveLength(2)
    expect(lines.join(' ')).toBe(eventTitle)
    expect(textAt(fake.texts, 144)).toBe('')
    expect(textAt(fake.texts, 149)).not.toBe('')
  })

  it('truncates event titles that would need more than two lines', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()
    const eventTitle = Array.from({ length: 40 }, (_, index) => `palabra${index}`).join(' ')

    await renderPreview(sheet, buildCertificate({ eventTitle }))

    const lines = titleDraws(fake.texts).map((draw) => draw.text)
    expect(lines).toHaveLength(2)
    expect(lines[0]?.endsWith('…')).toBe(false)
    expect(lines[1]?.endsWith('…')).toBe(true)
  })

  it('draws no title lines for an event without a title', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet, buildCertificate({ eventTitle: '   ' }))

    expect(titleDraws(fake.texts)).toHaveLength(0)
    expect(textAt(fake.texts, 144)).not.toBe('')
  })

  it('prints a single date when the event ends the day it starts or has no end', async () => {
    const sheet = await loadSheet()
    const expected = RANGE_FORMATTER.format(new Date(2025, 4, 10)).toLocaleUpperCase('es-ES')

    const sameDay = useFakeContext().fake
    await renderPreview(sheet, buildCertificate({ endsAt: '2025-05-10' }))
    expect(textAt(sameDay.texts, 144)).toBe(expected)

    const openEnded = useFakeContext().fake
    await renderPreview(sheet, buildCertificate({ endsAt: '' }))
    expect(textAt(openEnded.texts, 144)).toBe(expected)
  })

  it('omits the date when the start date is missing', async () => {
    const sheet = await loadSheet()
    const { fake } = useFakeContext()

    await renderPreview(sheet, buildCertificate({ startsAt: '', endsAt: '' }))

    expect(textAt(fake.texts, 144)).toBe('')
  })

  it('leaves the seal ring blank when its captions are empty', async () => {
    const sealFont = `500 ${(5.6 * PT_TO_MM).toFixed(4)}px`
    const sheet = await loadSheet()

    const captioned = useFakeContext().fake
    await renderPreview(sheet)
    expect(captioned.texts.filter((draw) => draw.font.startsWith(sealFont)).length).toBeGreaterThan(
      0,
    )

    const { i18n } = await import('@/shared/i18n')
    const sealMessages = { sealTop: '', sealBottom: '' }
    i18n.global.mergeLocaleMessage('es', {
      features: { account: { certificates: { sheet: sealMessages } } },
    } as never)
    const blank = useFakeContext().fake
    await renderPreview(sheet)

    expect(blank.texts.filter((draw) => draw.font.startsWith(sealFont))).toHaveLength(0)
  })

  it('skips the microtext border when its glyphs have no measurable width', async () => {
    const microFont = `500 ${(3.1 * PT_TO_MM).toFixed(4)}px`
    const sheet = await loadSheet()

    const normal = useFakeContext().fake
    await renderPreview(sheet)
    expect(normal.calls.filter((call) => call === 'clip')).toHaveLength(4)

    const zero = useFakeContext(
      createFakeContext({ zeroWidth: (font) => font.startsWith(microFont) }),
    ).fake
    await renderPreview(sheet)
    expect(zero.calls.filter((call) => call === 'clip')).toHaveLength(0)
    expect(zero.depth).toBe(0)
  })
})

describe('downloadCertificatePng', () => {
  it('renders at print resolution and downloads a PNG named after event and participant', async () => {
    const sheet = await loadSheet()
    const { fake, canvases } = useFakeContext()
    const png = new Blob(['png-bytes'], { type: 'image/png' })
    const toBlob = stubToBlob(png)
    const downloads = captureDownloads()

    await sheet.downloadCertificatePng(buildCertificate())

    expect(canvases[0]).toMatchObject({ width: 3508, height: 2480 })
    expect(fake.scales).toEqual([[3508 / 297, 2480 / 210]])
    expect(toBlob).toHaveBeenCalledWith(expect.any(Function), 'image/png')
    expect(downloads).toEqual([
      { name: 'diploma-hackathon-de-primavera-ada-lovelace.png', blob: png },
    ])
    expect(canvases[0]?.canvas.width).toBe(0)
    expect(canvases[0]?.canvas.height).toBe(0)
  })

  it('strips accents and symbols from the file name and falls back when nothing remains', async () => {
    const sheet = await loadSheet()
    useFakeContext()
    stubToBlob(new Blob(['png']))
    const downloads = captureDownloads()

    await sheet.downloadCertificatePng(
      buildCertificate({
        eventTitle: '¡Programación & Ñandú!',
        firstName: 'José',
        lastName: 'Pérez',
      }),
    )
    await sheet.downloadCertificatePng(buildCertificate({ eventTitle: '***' }))
    await sheet.downloadCertificatePng(buildCertificate({ eventTitle: 'a'.repeat(60) }))

    expect(downloads.map((download) => download.name)).toEqual([
      'diploma-programacion-nandu-jose-perez.png',
      'diploma-certificate-ada-lovelace.png',
      `diploma-${'a'.repeat(48)}-ada-lovelace.png`,
    ])
  })

  it('rejects and releases the canvas when the image cannot be encoded', async () => {
    const sheet = await loadSheet()
    const { canvases } = useFakeContext()
    stubToBlob(null)
    const downloads = captureDownloads()

    await expect(sheet.downloadCertificatePng(buildCertificate())).rejects.toThrow(
      t('features.account.certificates.renderError'),
    )

    expect(downloads).toHaveLength(0)
    expect(canvases[0]?.canvas.width).toBe(0)
  })

  it('releases the canvas when rendering fails', async () => {
    const sheet = await loadSheet()
    const created = vi.spyOn(document, 'createElement')
    const downloads = captureDownloads()

    await expect(sheet.downloadCertificatePng(buildCertificate())).rejects.toThrow(
      t('features.account.certificates.renderError'),
    )

    const canvas = created.mock.results[0]?.value as HTMLCanvasElement
    expect(canvas.width).toBe(0)
    expect(canvas.height).toBe(0)
    expect(downloads).toHaveLength(0)
  })
})

describe('downloadCertificatePdf', () => {
  function latin1(bytes: Uint8Array): string {
    return Buffer.from(bytes).toString('latin1')
  }

  it('downloads a landscape A4 PDF embedding the deflated RGB pixels', async () => {
    const sheet = await loadSheet()
    useFakeContext()
    const downloads = captureDownloads()

    await sheet.downloadCertificatePdf(buildCertificate())

    expect(downloads).toHaveLength(1)
    const [download] = downloads
    expect(download?.name).toBe('diploma-hackathon-de-primavera-ada-lovelace.pdf')
    expect(download?.blob.type).toBe('application/pdf')

    const bytes = new Uint8Array(await (download?.blob ?? new Blob()).arrayBuffer())
    const text = latin1(bytes)
    expect(text.startsWith('%PDF-1.4\n')).toBe(true)
    expect(text).toContain('/MediaBox [0 0 841.89 595.28]')
    expect(text).toContain('/Width 3508 /Height 2480')
    expect(text).toContain('q 841.89 0 0 595.28 0 0 cm /Im0 Do Q\n')
    expect(text.endsWith('%%EOF\n')).toBe(true)

    const header = /\/Length (\d+) >>\nstream\n/u.exec(text)
    expect(header).not.toBeNull()
    const length = Number(header?.[1])
    const start = (header?.index ?? 0) + (header?.[0].length ?? 0)
    const pixels = inflateSync(bytes.subarray(start, start + length))
    expect([...pixels]).toEqual([10, 20, 30, 40, 50, 60])
    expect(text.slice(start + length, start + length + 11)).toBe('\nendstream\n')

    const xrefStart = Number(/startxref\n(\d+)\n/u.exec(text)?.[1])
    expect(text.slice(xrefStart, xrefStart + 4)).toBe('xref')
    const offsets = [...text.slice(xrefStart).matchAll(/(\d{10}) 00000 n /gu)].map((match) =>
      Number(match[1]),
    )
    expect(offsets).toHaveLength(5)
    offsets.forEach((offset, index) => {
      expect(text.slice(offset, offset + 8)).toBe(`${index + 1} 0 obj\n`)
    })
  })

  it('rejects when the rendered canvas no longer provides a context', async () => {
    const sheet = await loadSheet()
    const fake = createFakeContext()
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext')
      .mockReturnValueOnce(fake.context)
      .mockReturnValueOnce(null)
    const downloads = captureDownloads()

    await expect(sheet.downloadCertificatePdf(buildCertificate())).rejects.toThrow(
      t('features.account.certificates.renderError'),
    )

    expect(downloads).toHaveLength(0)
  })
})
