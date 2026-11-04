import type { Locator, Page } from '@playwright/test'

/** A group of attendees of one role in the leader roster. */
export type AttendeeGroup = 'Usuarios' | 'Dependientes'

/**
 * The «Asistentes» tab of the event page, which a confirmed leader sees: a confidentiality note and,
 * per activity they lead, the confirmed attendees grouped by role, adults and minors apart.
 */
export class LeaderRoster {
  /** The note «Datos personales confidenciales». */
  readonly notice: Locator

  constructor(private readonly page: Page) {
    this.notice = page.getByRole('main').getByRole('note')
  }

  /** The block of the led activity titled `title`. */
  activity(title: string): Locator {
    return this.page
      .locator('article.lr-activity')
      .filter({ has: this.page.getByRole('heading', { level: 2, name: title, exact: true }) })
  }

  /** The section of `activity` that lists the attendees with the role `role`. */
  role(activity: Locator, role: string): Locator {
    return activity
      .locator('section.lr-role')
      .filter({ has: this.page.getByRole('heading', { level: 3, name: new RegExp(`^${role}`) }) })
  }

  /** The attendee of `group` in a role section whose entry shows `text`, such as a last name. */
  attendee(role: Locator, group: AttendeeGroup, text: string): Locator {
    return role
      .locator('.lr-group')
      .filter({ hasText: group })
      .getByRole('listitem')
      .filter({ hasText: text })
  }
}
