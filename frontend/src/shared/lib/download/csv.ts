import { ref } from 'vue'

const DELIMITER = ';'
const ROW_SEPARATOR = '\r\n'
const BYTE_ORDER_MARK = '\ufeff'
const FORMULA_TRIGGER = /^[=+\-@\t\r]/
const QUOTE_TRIGGER = /["\r\n]|^\s|\s$/

/** Cell value for CSV export; `null` and `undefined` become empty cells. */
export type CsvValue = string | null | undefined

function csvCell(value: CsvValue): string {
  const text = (value ?? '').replace(/\r\n|\r|\n/g, ROW_SEPARATOR)
  if (text === '') return ''
  const guarded = FORMULA_TRIGGER.test(text) ? `'${text}` : text
  if (!guarded.includes(DELIMITER) && !QUOTE_TRIGGER.test(guarded)) return guarded
  return `"${guarded.replace(/"/g, '""')}"`
}

function buildCsv(headers: readonly string[], rows: readonly CsvValue[][]): string {
  const lines = [headers, ...rows].map((row) => row.map(csvCell).join(DELIMITER))
  return `${BYTE_ORDER_MARK}${lines.join(ROW_SEPARATOR)}${ROW_SEPARATOR}`
}

/** Data source, column layout and outcome callbacks for `useCsvExport`. */
export interface CsvExportOptions<T> {
  readonly fetchRows: () => Promise<T[]>
  readonly headers: readonly string[]
  readonly toRow: (item: T) => CsvValue[]
  readonly filename: () => string
  readonly onExported: (rows: readonly T[]) => void
  readonly onError: (error: unknown) => void
}

/**
 * Fetches all rows and downloads them as an Excel-friendly CSV (UTF-8 BOM, `;` delimiter, CRLF).
 * Cells starting with `=`, `+`, `-` or `@` are prefixed with `'` to block formula injection.
 * `exporting` guards against concurrent runs; failures go to `onError` instead of throwing.
 */
export function useCsvExport<T>(options: CsvExportOptions<T>) {
  const exporting = ref(false)

  async function exportCsv(): Promise<void> {
    if (exporting.value) return
    exporting.value = true
    try {
      const rows = await options.fetchRows()
      downloadCsv(options.filename(), buildCsv(options.headers, rows.map(options.toRow)))
      options.onExported(rows)
    } catch (error) {
      options.onError(error)
    } finally {
      exporting.value = false
    }
  }

  return { exporting, exportCsv }
}

/** Saves a `Blob` in the browser through a temporary object URL and anchor click. */
export function downloadBlob(filename: string, blob: Blob): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.rel = 'noopener'
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

function downloadCsv(filename: string, content: string): void {
  downloadBlob(filename, new Blob([content], { type: 'text/csv;charset=utf-8' }))
}
