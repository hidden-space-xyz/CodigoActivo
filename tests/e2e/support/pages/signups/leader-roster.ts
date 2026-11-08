import type { Locator, Page } from '@playwright/test'

import { ROLE_LABELS, type RoleName } from '../../api/catalogs'

/** A group of attendees of one role in the leader roster: adults with an account or minors. */
export type AttendeeGroup = 'users' | 'dependents'

/**
 * The «Asistentes» tab of the event page, which a confirmed leader sees: a confidentiality note
 * and, per activity they lead, the confirmed attendees grouped by role, adults and minors apart.
 */
export class LeaderRoster {
  /** The note «Datos personales confidenciales». */
  readonly notice: Locator

  constructor(private readonly page: Page) {
    this.notice = page.getByTestId('page-event-detail').getByTestId('leader-roster-notice')
  }

  /** The block of the led activity titled `title`. */
  activity(title: string): Locator {
    return this.page.getByTestId('leader-roster-activity').filter({ hasText: title })
  }

  /** The section of `activity` that lists the attendees with the role `role`. */
  role(activity: Locator, role: RoleName): Locator {
    return activity.getByTestId('leader-roster-role').filter({
      has: this.page.getByTestId('leader-roster-role-name').filter({ hasText: ROLE_LABELS[role] }),
    })
  }

  /** The attendee of `group` in a role section whose entry shows `text`, such as a last name. */
  attendee(role: Locator, group: AttendeeGroup, text: string): Locator {
    return role
      .getByTestId(`leader-roster-${group}`)
      .getByTestId('leader-roster-person')
      .filter({ hasText: text })
  }
}
