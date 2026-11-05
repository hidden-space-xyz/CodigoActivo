import type { Locator, Page } from '@playwright/test'

/** A group of the history tab: the upcoming or the past events. */
export type HistoryGroup = 'upcoming' | 'past'

/** The tab «Historial»: the household's events grouped into upcoming and past ones. */
export class HistoryTab {
  constructor(private readonly page: Page) {}

  /** The group `name`. */
  group(name: HistoryGroup): Locator {
    return this.page.getByTestId(`history-group-${name}`)
  }

  /** The entry of the event titled `title` within `group`. */
  event(group: HistoryGroup, title: string): Locator {
    return this.group(group)
      .getByTestId('history-event')
      .filter({ has: this.page.getByTestId('history-event-title').filter({ hasText: title }) })
  }

  /** The button that shows or hides the activities of an entry. */
  toggle(entry: Locator): Locator {
    return entry.getByTestId('history-event-toggle')
  }

  /** The signup to the activity titled `title` of an expanded entry. */
  activity(entry: Locator, title: string): Locator {
    return entry.getByTestId('history-activity').filter({ hasText: title })
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
    this.root = page.getByTestId('rating-dialog')
    this.mostLiked = this.root.getByTestId('rating-dialog-most-liked')
    this.leastLiked = this.root.getByTestId('rating-dialog-least-liked')
    this.suggestions = this.root.getByTestId('rating-dialog-suggestions')
    this.submit = this.root.getByTestId('rating-dialog-submit')
    this.saveChanges = this.root.getByTestId('rating-dialog-save')
    this.remove = this.root.getByTestId('rating-dialog-delete')
    this.score = this.root.getByTestId('rating-dialog-score')
    this.stars = this.score.locator('.el-rate__item')
  }

  /** Gives the event `score` stars by clicking the star of that position. */
  async rate(score: number): Promise<void> {
    await this.stars.nth(score - 1).click()
  }
}
