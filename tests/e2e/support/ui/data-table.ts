import { expect, type Locator, type Page } from '@playwright/test'

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

  constructor(
    private readonly page: Page,
    scope: Locator | Page = page,
  ) {
    this.root = scope.locator('.data-table').first()
    this.count = this.root.locator('.data-table__count')
    this.chips = this.root.getByRole('list', { name: 'Filtros activos' })
  }

  /** Waits until the table is not fetching. */
  async expectSettled(): Promise<void> {
    await expect(this.root).toHaveAttribute('aria-busy', 'false')
  }

  /** Types into the toolbar search box named `label` and waits for the filtered rows. */
  async search(label: string, text: string): Promise<void> {
    const box = this.root.getByRole('textbox', { name: label })
    await box.fill(text)
    await box.press('Enter')
    await this.expectSettled()
  }

  /** The body row that contains `text`. */
  row(text: string | RegExp): Locator {
    return this.root.locator('.el-table__body').getByRole('row').filter({ hasText: text })
  }

  /** Runs the row action `action`, an inline button or an entry of the row menu named `menu`. */
  async rowAction(text: string | RegExp, action: string, menu?: string): Promise<void> {
    const row = this.row(text)
    if (menu === undefined) {
      await row.getByRole('button', { name: action, exact: true }).click()
      return
    }
    await row.getByRole('button', { name: menu }).click()
    await this.menuItem(action).click()
  }

  /** The entry `action` of the open row menu. */
  menuItem(action: string | RegExp): Locator {
    return this.page.locator('.row-actions__menu').getByRole('menuitem', { name: action })
  }

  /** Opens the header or toolbar filter `Filtrar por {label}` and picks `option`. */
  async filterBy(label: string, option: string): Promise<void> {
    const panel = await this.openFilter(`Filtrar por ${label}`)
    await panel.locator('.el-select').click()
    await this.page.getByRole('option', { name: option, exact: true }).click()
    await this.expectSettled()
  }

  /**
   * Types `text` into the header search «Buscar por {label}», applies it with Enter once the panel
   * has focused its box, and dismisses the panel with Escape, since the app reopens it right after
   * Enter closes it.
   */
  async searchColumn(label: string, text: string): Promise<void> {
    const panel = await this.enterColumnSearch(label, text)
    await this.expectSettled()
    await panel.locator('input').press('Escape')
    await expect(panel).toBeHidden()
  }

  /** Opens the header search «Buscar por {label}», types `text` and presses Enter in it. */
  async enterColumnSearch(label: string, text: string): Promise<Locator> {
    const panel = await this.openFilter(`Buscar por ${label}`)
    const box = panel.locator('input')
    await expect(box).toBeFocused()
    await box.fill(text)
    await box.press('Enter')
    return panel
  }

  /**
   * Opens the filter popover of the toggle named `toggle` and returns its panel. A click that lands
   * while the table re-renders can leave the popover closed, so the click is repeated until it opens.
   */
  private async openFilter(toggle: string): Promise<Locator> {
    const button = this.root.getByRole('button', { name: toggle, exact: true })
    const panel = this.page.locator('.column-filter__panel').filter({ visible: true })
    await expect(async () => {
      await button.click()
      await expect(panel).toBeVisible({ timeout: 2_000 })
    }).toPass({ timeout: 15_000 })
    return panel
  }
}
