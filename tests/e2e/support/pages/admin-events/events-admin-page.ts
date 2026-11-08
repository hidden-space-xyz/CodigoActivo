import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { chooseOption, fillPicker, setSwitch } from '../../ui/element-plus'
import { pickThumbnail, writeRichText } from '../../ui/forms'
import { AdminShell } from '../admin-shell'

/** Which events the table lists: those still to end, those ended or all of them. */
type EventScope = 'upcoming' | 'past' | 'all'

/** Days and moments typed into the event dialog, as `DD/MM/YYYY` and `DD/MM/YYYY HH:mm`. */
interface EventSchedule {
  readonly startsOn: string
  readonly endsOn: string
  readonly signupFrom: string
  readonly signupUntil: string
  readonly earlySignupFrom?: string
}

/** The events admin page, at `/admin/events`: the table, its scope switch and its dialogs. */
export class EventsAdminPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The admin table listing one event per row with its actions. */
  readonly table: DataTable

  /** The header button that opens the dialog to create an event. */
  readonly newEvent: Locator

  /** The switch between upcoming, past and all events. */
  readonly scope: Locator

  constructor(page: Page) {
    this.shell = new AdminShell(page)
    this.table = new DataTable(page)
    this.newEvent = this.shell.main.getByTestId('admin-events-new')
    this.scope = this.shell.main.getByTestId('admin-events-scope')
  }

  /** Opens the page and waits for the first list of events. */
  async open(): Promise<void> {
    await this.shell.open('admin-events')
    await this.table.expectSettled()
  }

  /** Narrows the table to the events whose title or subtitle contains `text`. */
  async find(text: string): Promise<void> {
    await this.table.search(text)
  }

  /** Switches the table to the events of `scope` by clicking its option. */
  async showScope(scope: EventScope): Promise<void> {
    await this.scope.getByTestId(`option-${scope}`).click()
    await this.table.expectSettled()
  }
}

/** The dialog that creates or edits an event. */
export class EventFormDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The title field. */
  readonly title: Locator

  /** The subtitle field. */
  readonly subtitle: Locator

  /** The multiple select of the event categories. */
  readonly categories: Locator

  /** The multiple select of the terms documents linked to the event. */
  readonly terms: Locator

  /** The field with the day the event starts. */
  readonly startsOn: Locator

  /** The field with the last day of the event. */
  readonly endsOn: Locator

  /** The field with the moment members and sponsors may start signing up, which is optional. */
  readonly earlySignupFrom: Locator

  /** The field with the moment everybody may start signing up. */
  readonly signupFrom: Locator

  /** The field with the moment signups close. */
  readonly signupUntil: Locator

  /** The footer button that saves the event. */
  readonly save: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('event-form-dialog')
    this.title = this.root.getByTestId('event-form-title')
    this.subtitle = this.root.getByTestId('event-form-subtitle')
    this.categories = this.root.getByTestId('event-form-categories')
    this.terms = this.root.getByTestId('event-form-terms')
    this.startsOn = this.root.getByTestId('event-form-starts-on')
    this.endsOn = this.root.getByTestId('event-form-ends-on')
    this.earlySignupFrom = this.root.getByTestId('event-form-early-signup-from')
    this.signupFrom = this.root.getByTestId('event-form-signup-from')
    this.signupUntil = this.root.getByTestId('event-form-signup-until')
    this.save = this.root.getByTestId('event-form-save')
  }

  /** Adds the category whose id is `id` to the event and closes the list of options. */
  async chooseCategory(id: string): Promise<void> {
    await chooseOption(this.categories, id)
    await this.closeOptions()
  }

  /**
   * Creates the category `name` through the button beside the select; the event takes it at once.
   */
  async createCategory(name: string): Promise<void> {
    await this.root.getByTestId('event-form-new-category').click()
    const dialog = this.page.getByTestId('new-category-dialog')
    await dialog.getByTestId('event-category-fields-name').fill(name)
    await dialog.getByTestId('new-category-dialog-create').click()
    await expect(dialog).toBeHidden()
  }

  /** Links the terms document whose id is `id` to the event and closes the list of options. */
  async chooseTerms(id: string): Promise<void> {
    await chooseOption(this.terms, id)
    await this.closeOptions()
  }

  /** The switch that makes the linked terms document `name` required. */
  termsRequired(name: string): Locator {
    return this.root
      .getByTestId('event-form-terms-row')
      .filter({ hasText: name })
      .getByTestId('event-form-terms-required')
  }

  /** Makes the linked terms document `name` required or optional. */
  async setTermsRequired(name: string, required: boolean): Promise<void> {
    await setSwitch(this.termsRequired(name), required)
  }

  /** Replaces the rich text of the description editor. */
  async describe(text: string): Promise<void> {
    await writeRichText(this.root.getByTestId('event-form-description'), text)
  }

  /** Types the days of the event and the moments of its signup windows. */
  async schedule(schedule: EventSchedule): Promise<void> {
    await fillPicker(this.startsOn, schedule.startsOn)
    await fillPicker(this.endsOn, schedule.endsOn)
    await fillPicker(this.signupFrom, schedule.signupFrom)
    await fillPicker(this.signupUntil, schedule.signupUntil)
    if (schedule.earlySignupFrom !== undefined) {
      await fillPicker(this.earlySignupFrom, schedule.earlySignupFrom)
    }
  }

  /** Picks the suite's image for the event. */
  async pickImage(): Promise<void> {
    await pickThumbnail(this.root)
  }

  /** Clicks the dialog title, which closes any open list of options. */
  private async closeOptions(): Promise<void> {
    await this.root.locator('.el-dialog__title').click()
  }
}
