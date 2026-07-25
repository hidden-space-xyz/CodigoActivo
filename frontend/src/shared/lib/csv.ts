const DELIMITER = ';'
const ROW_SEPARATOR = '\r\n'
const BYTE_ORDER_MARK = '\ufeff'
const FORMULA_TRIGGER = /^[=+\-@\t\r]/
const QUOTE_TRIGGER = /["\r\n]|^\s|\s$/

export type CsvValue = string | null | undefined

function csvCell(value: CsvValue): string {
  const text = (value ?? '').replace(/\r\n|\r|\n/g, ROW_SEPARATOR)
  if (text === '') return ''
  const guarded = FORMULA_TRIGGER.test(text) ? `'${text}` : text
  if (!guarded.includes(DELIMITER) && !QUOTE_TRIGGER.test(guarded)) return guarded
  return `"${guarded.replace(/"/g, '""')}"`
}

export function buildCsv(headers: readonly string[], rows: readonly CsvValue[][]): string {
  const lines = [headers, ...rows].map((row) => row.map(csvCell).join(DELIMITER))
  return `${BYTE_ORDER_MARK}${lines.join(ROW_SEPARATOR)}${ROW_SEPARATOR}`
}

export function downloadCsv(filename: string, content: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.rel = 'noopener'
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
