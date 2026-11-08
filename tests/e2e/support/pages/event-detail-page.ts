import { expect, type Locator, type Page } from '@playwright/test'
import type { ActivityResponse } from '@contract/activityResponse'
import type { EventResponse } from '@contract/eventResponse'

import { ActivityCard } from './signups/activity-card'

/** A tab of the public event page. */
export type EventTab = 'info' | 'activities' | 'attendees'

/**
 * The public page of an event, at `/events/{id}`: information, activities and the leader roster.
 */
export class EventDetailPage {
  /** The main landmark that holds the page. */
  readonly main: Locator

  constructor(private readonly page: Page) {
    this.main = page.getByTestId('page-event-detail')
  }

  /** Opens the page of the event `eventId` and waits for its title. */
  async goto(eventId: string, title: string): Promise<void> {
    await this.page.goto(`/events/${eventId}`)
    await expect(this.main.getByTestId('event-detail-title')).toHaveText(title)
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
    return this.main.getByTestId(`event-detail-tab-${name}`)
  }

  /** Opens the activities tab and waits for the timeline. */
  async openActivities(): Promise<void> {
    await this.tab('activities').click()
    await expect(this.main.getByTestId('event-activities-loading')).toBeHidden()
  }

  /** Opens the page of `event` on its activities tab and returns the card of `activity`. */
  async openActivity(event: EventResponse, activity: ActivityResponse): Promise<ActivityCard> {
    await this.goto(event.id, event.title)
    await this.openActivities()
    return new ActivityCard(this.activity(activity.title))
  }

  /** The card of the activity titled `title` in the activities tab. */
  activity(title: string): Locator {
    return this.main.getByTestId('activity-card').filter({ hasText: title })
  }
}
