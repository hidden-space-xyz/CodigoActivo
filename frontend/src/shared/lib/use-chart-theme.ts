import { computed } from 'vue'

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
