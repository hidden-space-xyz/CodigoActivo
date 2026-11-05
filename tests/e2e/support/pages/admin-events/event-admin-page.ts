import { expect, type Locator, type Page } from '@playwright/test'

import {
  CATALOG,
  type RoleName,
  type SignupStatusName,
  type UserTypeName,
} from '../../api/catalogs'
import { t } from '../../i18n'
import { DataTable } from '../../ui/data-table'
import { chooseOption } from '../../ui/element-plus'

/** A tab of the event admin page, by its name. */
type EventAdminTab = 'activities' | 'attendees' | 'opinions' | 'terms'

/** An activity modality: on site or online. */
type ModalityName = keyof typeof CATALOG.modalities

/** How the backend catalog names each activity role, as the interface shows it. */
export const ROLE_LABELS: Readonly<Record<RoleName, string>> = {
  leader: 'Líder',
  volunteer: 'Voluntario',
  participant: 'Participante',
}

/** How the backend catalog names each signup status, as the interface shows it. */
export const STATUS_LABELS: Readonly<Record<SignupStatusName, string>> = {
  requested: 'Solicitada',
  confirmed: 'Confirmada',
  denied: 'Rechazada',
}

/** How the backend catalog names each activity modality, as the interface shows it. */
export const MODALITY_LABELS: Readonly<Record<ModalityName, string>> = {
  onSite: 'Presencial',
  online: 'Online',
}

/** How the backend catalog names each type of user account, as the interface shows it. */
export const USER_TYPE_LABELS: Readonly<Record<UserTypeName, string>> = {
  member: 'Socio',
  sponsor: 'Patrocinador',
  participant: 'Participante',
}

/**
 * The admin page of one event, at `/admin/events/{id}`: its header with the print buttons, the
 * summary cards and the activities, attendees, opinions and terms tabs.
 */
export class EventAdminPage {
  /** The main landmark of the page. */
  readonly main: Locator

  /** The subtitle of the event, under its title. */
  readonly subtitle: Locator

  /** The table of the activities tab. */
  readonly activities: DataTable

  /** The button of the activities table that opens the dialog to create one. */
  readonly newActivity: Locator

  /** The header button that opens the badges printout. */
  readonly printBadges: Locator

  /** The header button that opens the attendance roster printout. */
  readonly printRoster: Locator

  constructor(private readonly page: Page) {
    this.main = page.getByTestId('page-admin-event-detail')
    this.subtitle = this.main.getByTestId('admin-page-header-subtitle')
    this.activities = new DataTable(page, this.panel('activities'))
    this.newActivity = this.main.getByTestId('event-detail-new-activity')
    this.printBadges = this.main.getByTestId('event-detail-print-badges')
    this.printRoster = this.main.getByTestId('event-detail-print-roster')
  }

  /** The path of the admin page of the event `eventId`. */
  static path(eventId: string): string {
    return `/admin/events/${eventId}`
  }

  /** Opens the page of the event `eventId` and waits for its title and its activities. */
  async goto(eventId: string, title: string): Promise<void> {
    await this.page.goto(EventAdminPage.path(eventId))
    await expect(this.heading(title)).toBeVisible()
    await this.activities.expectSettled()
  }

  /** The `h1` with the title of the event. */
  heading(title: string): Locator {
    return this.main.getByTestId('admin-page-header-title').filter({ hasText: title })
  }

  /** The header of the tab `name`. */
  tab(name: EventAdminTab): Locator {
    return this.main.getByTestId(`event-detail-tab-${name}`)
  }

  /** The panel of the tab `name`, visible while that tab is selected. */
  panel(name: EventAdminTab): Locator {
    return this.main.getByTestId(`event-detail-panel-${name}`)
  }

  /** Selects the tab `name` and waits for its panel. */
  async openTab(name: EventAdminTab): Promise<void> {
    await this.tab(name).click()
    await expect(this.panel(name)).toBeVisible()
  }

  /** The summary card whose caption is `label`, such as the activities or a role. */
  summaryCard(label: string): Locator {
    return this.main.getByTestId('event-detail-summary-card').filter({
      has: this.page.getByTestId('event-detail-summary-label').filter({ hasText: label }),
    })
  }

  /** The figure shown in the summary card captioned `label`. */
  summaryValue(label: string): Locator {
    return this.summaryCard(label).getByTestId('event-detail-summary-value')
  }
}

/** The dialog that creates or edits an activity. */
export class ActivityFormDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The title field. */
  readonly title: Locator

  /** The description textarea. */
  readonly description: Locator

  /** The modality select, on site or online. */
  readonly modality: Locator

  /** The location or platform field. */
  readonly location: Locator

  /** The field with the moment the activity starts. */
  readonly startsAt: Locator

  /** The field with the moment the activity ends. */
  readonly endsAt: Locator

  /** The footer button that saves the activity. */
  readonly save: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('activity-form-dialog')
    this.title = this.root.getByTestId('activity-form-title')
    this.description = this.root.getByTestId('activity-form-description')
    this.modality = this.root.getByTestId('activity-form-modality')
    this.location = this.root.getByTestId('activity-form-location')
    this.startsAt = this.root.getByTestId('activity-form-start')
    this.endsAt = this.root.getByTestId('activity-form-end')
    this.save = this.root.getByTestId('activity-form-save')
  }

  /** The number box with the number of people wanted in `role`. */
  capacity(role: RoleName): Locator {
    return this.root
      .getByTestId('activity-form-capacity')
      .filter({ hasText: ROLE_LABELS[role] })
      .locator('input')
  }

  /** Picks the modality `name`. */
  async chooseModality(name: ModalityName): Promise<void> {
    await chooseOption(this.modality, CATALOG.modalities[name])
  }
}

/** The attendees tab: everyone signed up for the event, with their signups and contact. */
export class AttendeesTab {
  /** The panel of the tab. */
  readonly root: Locator

  /** The search box over names, addresses and phones. */
  readonly search: Locator

  /** The number of attendees listed, such as «2 asistentes». */
  readonly count: Locator

  /** The button that writes an email to the attendees listed. */
  readonly sendEmail: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('event-detail-panel-attendees')
    this.search = this.root.getByTestId('event-attendees-search')
    this.count = this.root.getByTestId('event-attendees-count')
    this.sendEmail = this.root.getByTestId('event-attendees-send-email')
  }

  /** The entry of the attendee whose full name is `name`. */
  attendee(name: string): Locator {
    return this.root.getByTestId('event-attendee').filter({
      has: this.page.getByTestId('event-attendee-name').filter({ hasText: name }),
    })
  }

  /** The signup of the attendee `name` for the activity titled `activity`. */
  signup(name: string, activity: string): Locator {
    return this.attendee(name).getByTestId('event-attendee-signup').filter({ hasText: activity })
  }

  /** Opens the status change of a signup, picks `status` and applies it. */
  async changeStatus(name: string, activity: string, status: SignupStatusName): Promise<void> {
    await this.change(name, activity, 'status', CATALOG.signupStatuses[status])
  }

  /** Opens the role change of a signup, picks `role` and applies it. */
  async changeRole(name: string, activity: string, role: RoleName): Promise<void> {
    await this.change(name, activity, 'role', CATALOG.roles[role])
  }

  private async change(
    name: string,
    activity: string,
    kind: 'role' | 'status',
    option: string,
  ): Promise<void> {
    await this.signup(name, activity).getByTestId(`event-attendee-change-${kind}`).click()
    const dialog = this.page.getByTestId(`event-attendees-${kind}-dialog`)
    await expect(dialog.getByTestId('assignment-change-dialog-context')).toHaveText(
      t('pages.admin.eventDetail.attendees.dialogContext', { name, activity }),
    )
    await chooseOption(dialog.getByTestId('assignment-change-dialog-select'), option)
    await dialog.getByTestId('assignment-change-dialog-apply').click()
  }
}

/** The opinions tab: the ratings attendees left, without their authors. */
export class OpinionsTab {
  /** The panel of the tab. */
  readonly root: Locator

  /** The note the tab shows while it loads, when it fails or when nobody rated the event. */
  readonly empty: Locator

  /** The number of ratings, such as «1 valoración». */
  readonly count: Locator

  /** The ratings listed. */
  readonly ratings: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('event-detail-panel-opinions')
    this.empty = this.root.getByTestId('event-opinions-state')
    this.count = this.root.getByTestId('event-opinions-count')
    this.ratings = this.root.getByTestId('event-opinion')
  }
}

/** The terms tab: the decision taken for each person on each terms document. */
export class TermsTab {
  /** The panel of the tab. */
  readonly root: Locator

  /** The table of decisions. */
  readonly table: DataTable

  constructor(page: Page) {
    this.root = page.getByTestId('event-detail-panel-terms')
    this.table = new DataTable(page, this.root)
  }

  /** The row of the decision of the person `name` on the document `document`. */
  decision(name: string, document: string): Locator {
    return this.table.row(name).filter({ hasText: document })
  }
}
