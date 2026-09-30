import type { ChartPalette } from '@/shared/lib/chart'

/** Configuration passed to the fake `Chart` constructor. */
export interface FakeChartConfig {
  type: string
  data: unknown
  options: unknown
}

/** Every fake chart created since the last `resetFakeCharts()`, in creation order. */
export const fakeCharts: FakeChart[] = []

/** Arguments of every `Chart.register` call. */
export const registeredPlugins: unknown[][] = []

/** Minimal stand-in for Chart.js' `Chart`: records its canvas and config and whether it was destroyed. */
export class FakeChart {
  destroyed = false

  constructor(
    readonly canvas: HTMLCanvasElement,
    readonly config: FakeChartConfig,
  ) {
    fakeCharts.push(this)
  }

  static register(...plugins: unknown[]): void {
    registeredPlugins.push(plugins)
  }

  destroy(): void {
    this.destroyed = true
  }
}

/** Charts that were created and not destroyed yet. */
export function liveCharts(): FakeChart[] {
  return fakeCharts.filter((chart) => !chart.destroyed)
}

/** Forgets the charts created by previous tests. */
export function resetFakeCharts(): void {
  fakeCharts.length = 0
}

/** Module shape returned from `vi.mock('chart.js', ...)`. */
export const chartJsModule = {
  Chart: FakeChart,
  registerables: ['fake-registerable'],
}

/** Distinct, recognizable colors so assertions can tell which palette entry was used. */
export const TEST_PALETTE: ChartPalette = {
  orange: 'orange',
  orangeSoft: 'orange-soft',
  lime: 'lime',
  limeSoft: 'lime-soft',
  azure: 'azure',
  azureSoft: 'azure-soft',
  success: 'success',
  successSoft: 'success-soft',
  warning: 'warning',
  warningSoft: 'warning-soft',
  danger: 'danger',
  dangerSoft: 'danger-soft',
  text: 'text',
  textMuted: 'text-muted',
  textDim: 'text-dim',
  grid: 'grid',
  surface: 'surface',
  border: 'border',
}
