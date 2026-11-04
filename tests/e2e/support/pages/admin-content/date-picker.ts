import type { Locator, Page } from '@playwright/test'

import { dayFromToday } from '../../data/time'

/**
 * The cell of `day` (`YYYY-MM-DD`) in the open Element Plus date picker, which shows the current
 * month with the last days of the previous one and the first days of the next one around it.
 */
export function pickerCell(page: Page, day: string): Locator {
  const month = day.slice(0, 7)
  const shown = dayFromToday(0).slice(0, 7)
  const position =
    month === shown
      ? ':not(.prev-month):not(.next-month)'
      : month > shown
        ? '.next-month'
        : '.prev-month'
  return page
    .locator('.el-picker-panel')
    .filter({ visible: true })
    .locator(`td${position}`)
    .filter({ hasText: new RegExp(`^${Number(day.slice(8))}$`) })
}
