import { computed } from 'vue'
import type { ChartOptions, TooltipItem } from 'chart.js'

import { formatNumber } from './format'
import { useTheme } from './use-theme'

/** Resolved `--ca-*` color values for chart libraries that cannot read CSS variables. */
export interface ChartPalette {
  orange: string
  orangeSoft: string
  lime: string
  limeSoft: string
  azure: string
  azureSoft: string
  success: string
  successSoft: string
  warning: string
  warningSoft: string
  danger: string
  dangerSoft: string
  text: string
  textMuted: string
  textDim: string
  grid: string
  surface: string
  border: string
}

function cssVar(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

/** Reactive chart palette read from the document's computed styles, recomputed on theme changes. */
export function useChartTheme() {
  const { theme } = useTheme()

  const palette = computed<ChartPalette>(() => {
    void theme.value
    return {
      orange: cssVar('--ca-orange'),
      orangeSoft: cssVar('--ca-orange-soft'),
      lime: cssVar('--ca-lime'),
      limeSoft: cssVar('--ca-lime-soft'),
      azure: cssVar('--ca-azure'),
      azureSoft: cssVar('--ca-azure-soft'),
      success: cssVar('--ca-success'),
      successSoft: cssVar('--ca-success-soft'),
      warning: cssVar('--ca-warning'),
      warningSoft: cssVar('--ca-warning-soft'),
      danger: cssVar('--ca-danger'),
      dangerSoft: cssVar('--ca-danger-soft'),
      text: cssVar('--ca-text'),
      textMuted: cssVar('--ca-text-muted'),
      textDim: cssVar('--ca-text-dim'),
      grid: cssVar('--ca-grid-line'),
      surface: cssVar('--ca-surface'),
      border: cssVar('--ca-border'),
    }
  })

  return { palette }
}

/** Bottom legend with circular point markers, matching the app's chart styling. */
export function legend(palette: ChartPalette, display = true) {
  return {
    display,
    position: 'bottom' as const,
    labels: {
      color: palette.textMuted,
      usePointStyle: true,
      pointStyle: 'circle' as const,
      boxWidth: 8,
      boxHeight: 8,
      padding: 14,
      font: { size: 12 },
    },
  }
}

/** Shared tooltip box styling (surface background, muted body, rounded corners). */
export function tooltipBox(palette: ChartPalette) {
  return {
    backgroundColor: palette.surface,
    titleColor: palette.text,
    bodyColor: palette.textMuted,
    borderColor: palette.border,
    borderWidth: 1,
    padding: 10,
    cornerRadius: 8,
    usePointStyle: true,
  }
}

/** Numeric axis starting at zero, with grid lines and integer-precision muted ticks. */
export function valueScale(palette: ChartPalette, stacked: boolean) {
  return {
    stacked,
    beginAtZero: true,
    border: { display: false },
    grid: { color: palette.grid },
    ticks: { color: palette.textMuted, precision: 0, font: { size: 11 } },
  }
}

/** Category axis without grid lines, with muted labels that auto-skip when crowded. */
export function categoryScale(palette: ChartPalette, stacked: boolean, { autoSkip = true } = {}) {
  return {
    stacked,
    border: { display: false },
    grid: { display: false },
    ticks: {
      color: palette.textMuted,
      font: { size: 11 },
      maxRotation: 0,
      autoSkip,
      autoSkipPadding: 12,
    },
  }
}

/** Bar chart options; `stacked` stacks both axes. Tooltips show formatted numbers per dataset. */
export function barOptions(palette: ChartPalette, stacked: boolean): ChartOptions<'bar'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'bar'>) =>
            ` ${item.dataset.label ?? ''}: ${formatNumber(item.parsed.y)}`,
        },
      },
    },
    scales: { x: categoryScale(palette, stacked), y: valueScale(palette, stacked) },
  }
}

/**
 * Splits text into at most `maxLines` lines of up to `maxPerLine` characters for chart axis labels,
 * appending an ellipsis when words are cut off or a single word is too long.
 */
export function wrapLabel(text: string, maxPerLine = 26, maxLines = 2): string[] {
  const words = text.split(/\s+/).filter(Boolean)
  if (words.length === 0) return ['']

  const lines: string[] = []
  let current = ''
  for (const word of words) {
    const candidate = current ? `${current} ${word}` : word
    if (candidate.length <= maxPerLine) {
      current = candidate
    } else {
      if (current) lines.push(current)
      current = word
      if (lines.length >= maxLines) break
    }
  }
  if (lines.length < maxLines && current) lines.push(current)

  const truncated = lines.join(' ').length < text.replace(/\s+/g, ' ').length
  if (truncated && lines.length > 0) {
    const last = lines[lines.length - 1] ?? ''
    lines[lines.length - 1] =
      `${last.length > maxPerLine - 1 ? last.slice(0, maxPerLine - 1) : last}…`
  }
  return lines.map((line) =>
    line.length > maxPerLine ? `${line.slice(0, maxPerLine - 1)}…` : line,
  )
}
