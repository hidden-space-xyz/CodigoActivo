import type { ChartData, ChartOptions, TooltipItem } from 'chart.js'

import { formatBucketLabel, formatNumber, type ChartPalette } from '@/shared/lib'
import type {
  DashboardSliceResponse,
  DashboardTimeSeriesResponse,
} from '@/shared/api/generated/models'

export interface SeriesStyle {
  label: string
  color: (palette: ChartPalette) => string
  soft: (palette: ChartPalette) => string
}

type StyleMap = Record<string, SeriesStyle>

// Categorical hues are assigned per entity in a fixed order (never cycled) so the
// same key keeps its colour across charts and across a range change.
export const USER_TYPE_STYLE: StyleMap = {
  member: { label: 'Socios', color: (p) => p.orange, soft: (p) => p.orangeSoft },
  sponsor: { label: 'Patrocinadores', color: (p) => p.lime, soft: (p) => p.limeSoft },
  participant: { label: 'Participantes', color: (p) => p.azure, soft: (p) => p.azureSoft },
}

// Semantic status colours are reserved; they always ride with a legend label.
export const INSCRIPTION_STATUS_STYLE: StyleMap = {
  confirmed: { label: 'Confirmadas', color: (p) => p.success, soft: (p) => p.successSoft },
  requested: { label: 'Solicitadas', color: (p) => p.warning, soft: (p) => p.warningSoft },
  denied: { label: 'Rechazadas', color: (p) => p.danger, soft: (p) => p.dangerSoft },
}

export const AUDIENCE_STYLE: StyleMap = {
  adults: { label: 'Adultos', color: (p) => p.azure, soft: (p) => p.azureSoft },
  minors: { label: 'Menores', color: (p) => p.lime, soft: (p) => p.limeSoft },
}

export const RESOURCE_TYPE_STYLE: StyleMap = {
  internal: { label: 'Internos', color: (p) => p.azure, soft: (p) => p.azureSoft },
  external: { label: 'Externos', color: (p) => p.orange, soft: (p) => p.orangeSoft },
}

export const CONTENT_STYLE: StyleMap = {
  announcements: { label: 'Anuncios', color: (p) => p.orange, soft: (p) => p.orangeSoft },
  resources: { label: 'Recursos', color: (p) => p.azure, soft: (p) => p.azureSoft },
}

export const CALENDAR_STYLE: StyleMap = {
  past: { label: 'Pasados', color: (p) => p.azure, soft: (p) => p.azureSoft },
  upcoming: { label: 'Próximos', color: (p) => p.orange, soft: (p) => p.orangeSoft },
}

function axisLabels(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
): string[] {
  return (series?.buckets ?? []).map((bucket) => formatBucketLabel(bucket, granularity))
}

export function hasSeriesData(series: DashboardTimeSeriesResponse | undefined): boolean {
  return (series?.series ?? []).some((set) => (set.values ?? []).some((value) => value > 0))
}

export function hasSliceData(slices: DashboardSliceResponse[] | null | undefined): boolean {
  return (slices ?? []).some((slice) => (slice.count ?? 0) > 0)
}

export function stackedAreaData(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
): ChartData<'line'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key ?? '']
      const color = style?.color(palette) ?? palette.textDim
      return {
        label: style?.label ?? set.key ?? '',
        data: set.values ?? [],
        borderColor: color,
        backgroundColor: style?.soft(palette) ?? color,
        fill: true,
        tension: 0.35,
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 4,
        pointBackgroundColor: color,
      }
    }),
  }
}

export function barSeriesData(
  series: DashboardTimeSeriesResponse | undefined,
  granularity: string,
  palette: ChartPalette,
  styleMap: StyleMap,
): ChartData<'bar'> {
  return {
    labels: axisLabels(series, granularity),
    datasets: (series?.series ?? []).map((set) => {
      const style = styleMap[set.key ?? '']
      return {
        label: style?.label ?? set.key ?? '',
        data: set.values ?? [],
        backgroundColor: style?.color(palette) ?? palette.textDim,
        borderColor: palette.surface,
        borderWidth: 1.5,
        borderRadius: 3,
        borderSkipped: false,
      }
    }),
  }
}

export function doughnutData(
  slices: DashboardSliceResponse[] | null | undefined,
  palette: ChartPalette,
  styleMap?: StyleMap,
): ChartData<'doughnut'> {
  const items = (slices ?? []).filter((slice) => (slice.count ?? 0) > 0)
  return {
    labels: items.map(
      (slice) => styleMap?.[slice.key ?? '']?.label ?? slice.label ?? slice.key ?? '',
    ),
    datasets: [
      {
        data: items.map((slice) => slice.count ?? 0),
        backgroundColor: items.map(
          (slice) => styleMap?.[slice.key ?? '']?.color(palette) ?? slice.color ?? palette.textDim,
        ),
        borderColor: palette.surface,
        borderWidth: 2,
        hoverOffset: 6,
      },
    ],
  }
}

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

export function rankingBarData(
  labels: (string | string[])[],
  values: number[],
  color: string,
  palette: ChartPalette,
): ChartData<'bar'> {
  return {
    labels,
    datasets: [
      {
        label: 'Inscripciones confirmadas',
        data: values,
        backgroundColor: color,
        borderColor: palette.surface,
        borderWidth: 1.5,
        borderRadius: 4,
        borderSkipped: false,
      },
    ],
  }
}

function legend(palette: ChartPalette, display = true) {
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

function tooltipBox(palette: ChartPalette) {
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

function valueScale(palette: ChartPalette, stacked: boolean) {
  return {
    stacked,
    beginAtZero: true,
    border: { display: false },
    grid: { color: palette.grid },
    ticks: { color: palette.textMuted, precision: 0, font: { size: 11 } },
  }
}

function categoryScale(palette: ChartPalette, stacked: boolean, { autoSkip = true } = {}) {
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

export function areaOptions(palette: ChartPalette): ChartOptions<'line'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'line'>) =>
            ` ${item.dataset.label ?? ''}: ${formatNumber(item.parsed.y)}`,
        },
      },
    },
    scales: { x: categoryScale(palette, true), y: valueScale(palette, true) },
  }
}

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

export function rankingOptions(palette: ChartPalette, fullLabels: string[]): ChartOptions<'bar'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    plugins: {
      legend: legend(palette, false),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          title: (items: TooltipItem<'bar'>[]) => fullLabels[items[0]?.dataIndex ?? 0] ?? '',
          label: (item: TooltipItem<'bar'>) => ` ${formatNumber(item.parsed.x)} confirmadas`,
        },
      },
    },
    scales: {
      x: valueScale(palette, false),
      y: categoryScale(palette, false, { autoSkip: false }),
    },
  }
}

export function doughnutOptions(palette: ChartPalette): ChartOptions<'doughnut'> {
  return {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '62%',
    plugins: {
      legend: legend(palette),
      tooltip: {
        ...tooltipBox(palette),
        callbacks: {
          label: (item: TooltipItem<'doughnut'>) => {
            const data = item.dataset.data as number[]
            const total = data.reduce(
              (sum, value, index) =>
                item.chart.getDataVisibility(index) ? sum + (value ?? 0) : sum,
              0,
            )
            const value = item.parsed
            const percent = total > 0 ? Math.round((value / total) * 100) : 0
            return ` ${item.label}: ${formatNumber(value)} (${percent}%)`
          },
        },
      },
    },
  }
}
