import { i18n } from '@/shared/i18n'

const numberFormatter = new Intl.NumberFormat('es-ES')

const megabyteFormatter = new Intl.NumberFormat('es-ES', {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
})

/** Spanish-locale number with thousands separators, or `—` for missing or `NaN` values. */
export function formatNumber(value?: number | null): string {
  if (value == null || Number.isNaN(value)) return '—'
  return numberFormatter.format(value)
}

/** Localized size in B, whole KB or one-decimal MB (1024-based); `—` for negative or NaN input. */
export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '—'
  if (bytes < 1024) return i18n.global.t('common.fileSize.bytes', { value: formatNumber(bytes) })

  const kilobytes = Math.round(bytes / 1024)
  if (kilobytes < 1024) {
    return i18n.global.t('common.fileSize.kilobytes', { value: formatNumber(kilobytes) })
  }

  return i18n.global.t('common.fileSize.megabytes', {
    value: megabyteFormatter.format(bytes / (1024 * 1024)),
  })
}

/** Rounds a percentage change and prefixes positives with `+` (e.g. `+12%`, `-3%`, `0%`). */
export function formatSignedPercent(value: number): string {
  if (!Number.isFinite(value)) return '—'
  const rounded = Math.round(value)
  const sign = rounded > 0 ? '+' : ''
  return `${sign}${rounded}%`
}
