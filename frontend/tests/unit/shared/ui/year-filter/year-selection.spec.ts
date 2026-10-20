import { describe, expect, it } from 'vitest'

import { yearSelection } from '@/shared/ui/year-filter'

describe('yearSelection', () => {
  it('keeps the selected year while it is listed', () => {
    expect(yearSelection(['2025', '2024'], '2024')).toBe('2024')
  })

  it('falls back to the first year when nothing or an unlisted year is selected', () => {
    expect(yearSelection(['2025', '2024'], '')).toBe('2025')
    expect(yearSelection(['2025', '2024'], '2023')).toBe('2025')
  })

  it('selects nothing without years', () => {
    expect(yearSelection([], '2024')).toBe('')
  })
})
