import { expect, type Locator, type Page } from '@playwright/test'

/** A tab of the public event page. */
export type EventTab = 'Información' | 'Actividades' | 'Asistentes'

/** The public page of an event, at `/events/{id}`: information, activities and the leader roster. */
export class EventDetailPage {
  /** The main landmark that holds the page. */
  readonly main: Locator

  constructor(private readonly page: Page) {
    this.main = page.getByRole('main')
  }

  /** Opens the page of the event `eventId` and waits for its title. */
  async goto(eventId: string, title: string): Promise<void> {
    await this.page.goto(`/events/${eventId}`)
    await expect(this.main.getByRole('heading', { level: 1, name: title })).toBeVisible()
  }

  /**
   * Opens the page of the event `eventId` as a signed-in person and waits until the API has
   * answered which of its activities they lead, which decides whether the attendees tab shows.
   */
  async gotoSignedIn(eventId: string, title: string): Promise<void> {
    const roster = this.page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === `/api/events/${eventId}/leader-roster` &&
        response.ok(),
    )
    await this.goto(eventId, title)
    await roster
  }

  /** The button that opens the tab `name`. */
  tab(name: EventTab): Locator {
    return this.main.getByRole('button', { name, exact: true })
  }

  /** Opens the activities tab and waits for the timeline. */
  async openActivities(): Promise<void> {
    await this.tab('Actividades').click()
    await expect(this.main.getByText('Cargando actividades…')).toBeHidden()
  }

  /** The card of the activity titled `title` in the activities tab. */
  activity(title: string): Locator {
    return this.main
      .locator('article.act')
      .filter({ has: this.page.getByRole('heading', { name: title, exact: true }) })
  }
}
