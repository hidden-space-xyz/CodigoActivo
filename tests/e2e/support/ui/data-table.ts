import { expect, type Locator, type Page } from '@playwright/test'

import { dayFromToday } from '../data/time'
import { chooseOption } from './element-plus'

/**
 * An admin table (`shared/ui/data-table`): a toolbar with an optional search box, filter chips, the
 * result count, the rows and their actions. `aria-busy` tells when it has finished fetching.
 */
export class DataTable {
  /** The card that holds the whole table. */
  readonly root: Locator

  /** The result count, such as «3 resultados». */
  readonly count: Locator

  /** The list of active filter chips. */
  readonly chips: Locator

  /** The button that clears every filter. */
  readonly clear: Locator

  constructor(
    private readonly page: Page,
    scope: Locator | Page = page,
  ) {
    this.root = scope.getByTestId('data-table').first()
    this.count = this.root.getByTestId('data-table-count')
    this.chips = this.root.getByTestId('data-table-chips')
    this.clear = this.root.getByTestId('data-table-clear')
  }

  /** Waits until the table is not fetching. */
  async expectSettled(): Promise<void> {
    await expect(this.root).toHaveAttribute('aria-busy', 'false')
  }

  /** Types into the toolbar search box and waits for the filtered rows. */
  async search(text: string): Promise<void> {
    const box = this.root.getByTestId('data-table-search')
    await box.fill(text)
    await box.press('Enter')
    await this.expectSettled()
  }

  /** The filter chip that shows `text`. */
  chip(text: string | RegExp): Locator {
    return this.chips.getByTestId('data-table-chip').filter({ hasText: text })
  }

  /** The body row that contains `text`. */
  row(text: string | RegExp): Locator {
    return this.root.locator('.el-table__body tr').filter({ hasText: text })
  }

  /**
   * Runs the row action `key` (the `key` of its `RowAction`) of the row that contains `text`: its
   * inline button, or else the entry of the row menu.
   */
  async rowAction(text: string | RegExp, key: string): Promise<void> {
    const row = this.row(text)
    const inline = row.getByTestId(`row-action-${key}`)
    if ((await inline.count()) > 0) {
      await inline.click()
      return
    }
    await row.getByTestId('row-actions-menu').click()
    await this.menuItem(key).click()
  }

  /** The entry `key` of the open row menu. */
  menuItem(key: string): Locator {
    return this.page
      .locator('.row-actions__menu')
      .filter({ visible: true })
      .getByTestId(`row-action-${key}`)
  }

  /**
   * Opens the column or toolbar filter that carries the test id `filter` and picks `value`. The
   * rows of the previous filter may still arrive and close the popover, so that is repeated if it
   * does.
   */
  async filterBy(filter: string, value: string): Promise<void> {
    await expect(async () => {
      const panel = await this.openFilter(filter)
      await chooseOption(panel.getByTestId('column-filter-select'), value, { timeout: 2_000 })
    }).toPass({ timeout: 15_000 })
    await this.expectSettled()
  }

  /**
   * Types `text` into the column search that carries the test id `search` and applies it with Enter
   * once the panel has focused its box, which also closes the panel.
   */
  async searchColumn(search: string, text: string): Promise<void> {
    const panel = await this.openFilter(search)
    const box = panel.getByTestId('column-filter-search')
    await expect(box).toBeFocused()
    await box.fill(text)
    await box.press('Enter')
    await this.expectSettled()
    await expect(panel).toBeHidden()
  }

  /**
   * Narrows the date range filter that carries the test id `filter` to the single `day`
   * (`YYYY-MM-DD`): the left calendar of the range picker goes back from the current month to the
   * month of `day`, a year or a month a click, and the day is picked as both ends of the range.
   */
  async filterByDay(filter: string, day: string): Promise<void> {
    const panel = await this.openFilter(filter)
    await panel.locator('.column-filter-date').click()
    const calendar = this.page
      .locator('.el-date-range-picker__content.is-left')
      .filter({ visible: true })
    const [year = 0, month = 0] = day.split('-').map(Number)
    const [thisYear = 0, thisMonth = 0] = dayFromToday(0).split('-').map(Number)
    const monthsBack = (thisYear - year) * 12 + thisMonth - month
    for (let step = 0; step < Math.floor(monthsBack / 12); step++) {
      await calendar.locator('.d-arrow-left').click()
    }
    for (let step = 0; step < monthsBack % 12; step++) {
      await calendar.locator('.arrow-left').click()
    }
    const cell = calendar
      .locator('td.available:not(.prev-month):not(.next-month)')
      .filter({ hasText: new RegExp(`^${Number(day.slice(8))}$`) })
    await cell.click()
    await cell.click()
    await this.expectSettled()
  }

  /**
   * Opens the popover of the filter that carries the test id `filter` and returns its panel. A
   * click that lands while the table re-renders can leave the popover closed, so the click is
   * repeated until it opens.
   */
  private async openFilter(filter: string): Promise<Locator> {
    const button = this.root.getByTestId(filter).getByTestId('column-filter-toggle')
    const panel = this.page.getByTestId('column-filter-panel').filter({ visible: true })
    await expect(async () => {
      await button.click()
      await expect(panel).toBeVisible({ timeout: 2_000 })
    }).toPass({ timeout: 15_000 })
    return panel
  }
}
