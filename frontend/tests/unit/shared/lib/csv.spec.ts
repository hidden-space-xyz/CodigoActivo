import { describe, expect, it, vi } from 'vitest'

import { downloadBlob, useCsvExport, type CsvValue } from '@/shared/lib'

interface Download {
  filename: string
  blob: Blob
}

function captureDownloads(): Download[] {
  const downloads: Download[] = []
  let lastBlob: Blob | undefined
  vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
    lastBlob = blob as Blob
    return 'blob:captured'
  })
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    if (lastBlob) downloads.push({ filename: this.download, blob: lastBlob })
  })
  return downloads
}

interface Row {
  name: CsvValue
  note: CsvValue
}

describe('downloadBlob', () => {
  it('clicks a temporary anchor and revokes the object URL afterwards', () => {
    vi.useFakeTimers()
    const revoke = vi.spyOn(URL, 'revokeObjectURL')
    const anchors: HTMLAnchorElement[] = []
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:file')
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      anchors.push(this)
      expect(document.body.contains(this)).toBe(true)
    })

    downloadBlob('report.pdf', new Blob(['x']))

    const [anchor] = anchors
    expect(anchor?.download).toBe('report.pdf')
    expect(anchor?.href).toBe('blob:file')
    expect(anchor?.rel).toBe('noopener')
    expect(document.body.querySelector('a')).toBeNull()
    expect(revoke).not.toHaveBeenCalled()

    vi.runAllTimers()
    expect(revoke).toHaveBeenCalledWith('blob:file')
  })
})

describe('useCsvExport', () => {
  it('downloads an Excel-friendly CSV and reports the exported rows', async () => {
    const downloads = captureDownloads()
    const rows: Row[] = [
      { name: 'Ada', note: 'Plain' },
      { name: '=SUM(A1)', note: 'semi;colon' },
      { name: '+34 600', note: 'He said "hi"' },
      { name: '-1', note: 'line\nbreak' },
      { name: '@cmd', note: ' padded ' },
      { name: null, note: undefined },
    ]
    const onExported = vi.fn()
    const onError = vi.fn()

    const { exporting, exportCsv } = useCsvExport<Row>({
      fetchRows: () => Promise.resolve(rows),
      headers: ['Name', 'Note'],
      toRow: (row) => [row.name, row.note],
      filename: () => 'people.csv',
      onExported,
      onError,
    })

    const running = exportCsv()
    expect(exporting.value).toBe(true)
    await running

    expect(exporting.value).toBe(false)
    expect(onExported).toHaveBeenCalledWith(rows)
    expect(onError).not.toHaveBeenCalled()
    expect(downloads).toHaveLength(1)

    const [download] = downloads
    expect(download?.filename).toBe('people.csv')
    expect(download?.blob.type).toBe('text/csv;charset=utf-8')
    const bytes = new Uint8Array(await (download as Download).blob.arrayBuffer())
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf])
    const text = new TextDecoder().decode(bytes.slice(3))
    expect(text).toBe(
      [
        'Name;Note',
        'Ada;Plain',
        `'=SUM(A1);"semi;colon"`,
        `'+34 600;"He said ""hi"""`,
        `'-1;"line\r\nbreak"`,
        `'@cmd;" padded "`,
        ';',
        '',
      ].join('\r\n'),
    )
  })

  it('ignores concurrent runs while an export is in progress', async () => {
    captureDownloads()
    let release: (rows: Row[]) => void = () => undefined
    const fetchRows = vi.fn(
      () =>
        new Promise<Row[]>((resolve) => {
          release = resolve
        }),
    )
    const { exportCsv } = useCsvExport<Row>({
      fetchRows,
      headers: ['Name'],
      toRow: (row) => [row.name],
      filename: () => 'x.csv',
      onExported: vi.fn(),
      onError: vi.fn(),
    })

    const first = exportCsv()
    await exportCsv()
    release([])
    await first

    expect(fetchRows).toHaveBeenCalledTimes(1)
  })

  it('passes failures to onError and resets the exporting flag', async () => {
    const downloads = captureDownloads()
    const failure = new Error('Network')
    const onError = vi.fn()
    const onExported = vi.fn()
    const { exporting, exportCsv } = useCsvExport<Row>({
      fetchRows: () => Promise.reject(failure),
      headers: ['Name'],
      toRow: (row) => [row.name],
      filename: () => 'x.csv',
      onExported,
      onError,
    })

    await exportCsv()

    expect(onError).toHaveBeenCalledWith(failure)
    expect(onExported).not.toHaveBeenCalled()
    expect(downloads).toHaveLength(0)
    expect(exporting.value).toBe(false)
  })
})
