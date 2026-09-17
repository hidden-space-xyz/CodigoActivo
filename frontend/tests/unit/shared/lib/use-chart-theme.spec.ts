import { describe, expect, it } from 'vitest'

import {
  barOptions,
  categoryScale,
  legend,
  tooltipBox,
  useChartTheme,
  useTheme,
  valueScale,
  wrapLabel,
} from '@/shared/lib'

import { TEST_PALETTE } from '../../../support/fixtures/public-dashboard/builders'

interface TestableBarOptions {
  plugins: {
    legend: { display: boolean; labels: { color: string } }
    tooltip: { backgroundColor: string; callbacks: { label: (item: unknown) => string } }
  }
  scales: Record<string, { stacked: boolean }>
}

describe('useChartTheme', () => {
  it('reads the palette from the root CSS variables', () => {
    const root = document.documentElement.style
    root.setProperty('--ca-orange', ' #ff6600 ')
    root.setProperty('--ca-grid-line', 'rgba(0, 0, 0, 0.1)')
    root.setProperty('--ca-text', '#111111')

    const { palette } = useChartTheme()

    expect(palette.value.orange).toBe('#ff6600')
    expect(palette.value.grid).toBe('rgba(0, 0, 0, 0.1)')
    expect(palette.value.text).toBe('#111111')
    expect(palette.value.azure).toBe('')
    expect(Object.keys(palette.value)).toHaveLength(18)
    root.cssText = ''
  })

  it('recomputes the palette when the theme changes', () => {
    const root = document.documentElement.style
    const { setTheme } = useTheme()
    setTheme('light')
    root.setProperty('--ca-surface', '#ffffff')
    const { palette } = useChartTheme()
    expect(palette.value.surface).toBe('#ffffff')

    root.setProperty('--ca-surface', '#000000')
    setTheme('dark')

    expect(palette.value.surface).toBe('#000000')
    root.cssText = ''
  })
})

describe('legend', () => {
  it('shows a bottom legend with circular point markers by default', () => {
    const options = legend(TEST_PALETTE)

    expect(options).toMatchObject({
      display: true,
      position: 'bottom',
      labels: { color: 'text-muted', usePointStyle: true, pointStyle: 'circle' },
    })
  })

  it('can be hidden while keeping the same styling', () => {
    expect(legend(TEST_PALETTE, false).display).toBe(false)
  })
})

describe('tooltipBox', () => {
  it('styles the tooltip box from the palette', () => {
    expect(tooltipBox(TEST_PALETTE)).toMatchObject({
      backgroundColor: 'surface',
      titleColor: 'text',
      bodyColor: 'text-muted',
      borderColor: 'border',
      usePointStyle: true,
    })
  })
})

describe('valueScale', () => {
  it('starts at zero and uses the palette for grid and ticks', () => {
    expect(valueScale(TEST_PALETTE, true)).toMatchObject({
      stacked: true,
      beginAtZero: true,
      grid: { color: 'grid' },
      ticks: { color: 'text-muted', precision: 0 },
    })
  })

  it('carries the stacked flag through unstacked charts too', () => {
    expect(valueScale(TEST_PALETTE, false).stacked).toBe(false)
  })
})

describe('categoryScale', () => {
  it('hides the grid and auto-skips crowded labels by default', () => {
    expect(categoryScale(TEST_PALETTE, true)).toMatchObject({
      stacked: true,
      grid: { display: false },
      ticks: { color: 'text-muted', autoSkip: true, maxRotation: 0 },
    })
  })

  it('can disable auto-skip, e.g. for a fixed ranking axis', () => {
    expect(categoryScale(TEST_PALETTE, false, { autoSkip: false }).ticks.autoSkip).toBe(false)
  })
})

describe('barOptions', () => {
  it.each([true, false])('applies stacked=%s to both axes and shows the legend', (stacked) => {
    const options = barOptions(TEST_PALETTE, stacked) as unknown as TestableBarOptions

    expect(options.scales.x?.stacked).toBe(stacked)
    expect(options.scales.y?.stacked).toBe(stacked)
    expect(options.plugins.legend.display).toBe(true)
    expect(options.plugins.tooltip.backgroundColor).toBe('surface')
  })

  it('formats the tooltip label with the dataset label and a localized number', () => {
    const { label } = (barOptions(TEST_PALETTE, true) as unknown as TestableBarOptions).plugins
      .tooltip.callbacks

    expect(label({ dataset: { label: 'Confirmadas' }, parsed: { y: 12345 } })).toBe(
      ' Confirmadas: 12.345',
    )
    expect(label({ dataset: {}, parsed: { y: 0 } })).toBe(' : 0')
  })
})

describe('wrapLabel', () => {
  it('returns a single empty line for blank text', () => {
    expect(wrapLabel('')).toEqual([''])
    expect(wrapLabel('   ')).toEqual([''])
  })

  it('does not split short text that fits on one line', () => {
    expect(wrapLabel('Taller de robótica')).toEqual(['Taller de robótica'])
  })

  it('splits long text across the allowed lines, adding an ellipsis when words are cut off', () => {
    expect(wrapLabel('one two three four five six', 10, 2)).toEqual(['one two', 'three fou…'])
  })

  it('shortens a single word longer than the maximum line width', () => {
    expect(wrapLabel('Supercalifragilistico', 10)).toEqual(['Supercali…'])
  })
})
