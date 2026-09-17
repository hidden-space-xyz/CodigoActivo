import { describe, expect, it } from 'vitest'

import { useChartTheme, useTheme } from '@/shared/lib'

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
