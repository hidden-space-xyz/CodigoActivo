import type { EventResponse } from '@contract/eventResponse'

import type { ApiClient } from './api-client'
import type { ContentSeeder, EventOptions, SignupWindowName } from './content'

const RUNNING_SINCE_DAYS = -60
const RUNNING_UNTIL_DAYS = 3

/**
 * Creates the events the public pages need through the seeder (events that already ended, and
 * events running for weeks that list before any other upcoming event) and deletes every event it
 * created once the test ends, so lists that only show the first few events stay free for the next
 * run.
 */
export class PublicEvents {
  private readonly created: string[] = []

  constructor(
    private readonly admin: ApiClient,
    private readonly seed: ContentSeeder,
  ) {}

  /**
   * A one-day event `startsIn` days from today (a negative number) whose signup closed before it.
   */
  past(startsIn: number, options: EventOptions = {}): Promise<EventResponse> {
    return this.track(this.seed.event({ ...options, startsIn }))
  }

  /**
   * An event that started two months ago and ends in three days, so it lists before the events
   * other tests create, with its signup in the state `signup` names.
   */
  running(signup: SignupWindowName, options: EventOptions = {}): Promise<EventResponse> {
    return this.track(
      this.seed.event({
        ...options,
        startsIn: RUNNING_SINCE_DAYS,
        lastsDays: RUNNING_UNTIL_DAYS - RUNNING_SINCE_DAYS + 1,
        signup,
      }),
    )
  }

  /** Creates `count` events through the seeder, all with `options`, and deletes them later. */
  async many(count: number, options: EventOptions): Promise<EventResponse[]> {
    const events = await Promise.all(Array.from({ length: count }, () => this.seed.event(options)))
    this.created.push(...events.map((event) => event.id))
    return events
  }

  /** Deletes every event this helper created. */
  async dispose(): Promise<void> {
    const ids = this.created.splice(0)
    await Promise.all(ids.map((id) => this.admin.delete(`/api/events/${id}`)))
  }

  private async track(pending: Promise<EventResponse>): Promise<EventResponse> {
    const event = await pending
    this.created.push(event.id)
    return event
  }
}
