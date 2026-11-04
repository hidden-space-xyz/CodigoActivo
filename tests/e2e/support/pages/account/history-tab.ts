import type { Locator, Page } from '@playwright/test'

/** A group of the history tab. */
export type HistoryGroup = 'Próximos eventos' | 'Eventos anteriores'

/** The tab «Historial»: the household's events grouped into upcoming and past ones. */
export class HistoryTab {
  constructor(private readonly page: Page) {}

  /** The group under the heading `label`. */
  group(label: HistoryGroup): Locator {
    return this.page
      .locator('.acc-history__group')
      .filter({ has: this.page.getByRole('heading', { level: 3, name: label, exact: true }) })
  }

  /** The entry of the event titled `title` within `group`. */
  event(group: HistoryGroup, title: string): Locator {
    return this.group(group)
      .locator('li.acc-history__event')
      .filter({ has: this.page.getByText(title, { exact: true }) })
  }

  /** The button that shows or hides the activities of an entry. */
  toggle(entry: Locator): Locator {
    return entry.locator('button.acc-history__toggle')
  }

  /** The signup to the activity titled `title` of an expanded entry. */
  activity(entry: Locator, title: string): Locator {
    return entry
      .locator('li.acc-history__activity')
      .filter({ has: this.page.getByText(title, { exact: true }) })
  }
}

/** The dialog «Valorar evento». */
export class RatingDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «¿Qué fue lo que MÁS te gustó?». */
  readonly mostLiked: Locator

  /** «¿Qué fue lo que MENOS te gustó?». */
  readonly leastLiked: Locator

  /** «¿Tienes alguna sugerencia para eventos FUTUROS?». */
  readonly suggestions: Locator

  /** «Enviar valoración», for a first rating. */
  readonly submit: Locator

  /** «Guardar cambios», for a stored rating. */
  readonly saveChanges: Locator

  /** «Borrar valoración», for a stored rating. */
  readonly remove: Locator

  /** The star control, which reports the chosen score in `aria-valuenow`. */
  readonly score: Locator

  private readonly stars: Locator

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Valorar evento' })
    this.mostLiked = this.root.getByLabel('¿Qué fue lo que MÁS te gustó?')
    this.leastLiked = this.root.getByLabel('¿Qué fue lo que MENOS te gustó?')
    this.suggestions = this.root.getByLabel('¿Tienes alguna sugerencia para eventos FUTUROS?')
    this.submit = this.root.getByRole('button', { name: 'Enviar valoración' })
    this.saveChanges = this.root.getByRole('button', { name: 'Guardar cambios' })
    this.remove = this.root.getByRole('button', { name: 'Borrar valoración' })
    this.score = this.root.locator('.el-rate')
    this.stars = this.score.locator('.el-rate__item')
  }

  /** Gives the event `score` stars by clicking the star of that position. */
  async rate(score: number): Promise<void> {
    await this.stars.nth(score - 1).click()
  }
}
