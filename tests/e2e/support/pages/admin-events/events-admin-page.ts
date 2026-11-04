import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { chooseOption, fillPicker, setSwitch } from '../../ui/element-plus'
import { pickThumbnail, writeRichText } from '../../ui/forms'
import { AdminShell } from '../admin-shell'

/** Which events the table lists: those still to end, those ended or all of them. */
type EventScopeLabel = 'Próximos' | 'Pasados' | 'Todos'

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

  /** The «Mostrar eventos» switch between upcoming, past and all events. */
  readonly scope: Locator

  constructor(page: Page) {
    this.shell = new AdminShell(page)
    this.table = new DataTable(page)
    this.newEvent = this.shell.main.getByRole('button', { name: 'Nuevo evento' })
    this.scope = page.getByRole('radiogroup', { name: 'Mostrar eventos' })
  }

  /** Opens the page and waits for the first list of events. */
  async open(): Promise<void> {
    await this.shell.open('Eventos')
    await this.table.expectSettled()
  }

  /** Narrows the table to the events whose title or subtitle contains `text`. */
  async find(text: string): Promise<void> {
    await this.table.search('Buscar por título o subtítulo', text)
  }

  /** Switches the scope of the table by clicking the text of the option. */
  async showScope(scope: EventScopeLabel): Promise<void> {
    await this.scope.getByText(scope, { exact: true }).click()
    await this.table.expectSettled()
  }
}

/** The dialog that creates («Nuevo evento») or edits («Editar evento») an event. */
export class EventFormDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The «Título» field. */
  readonly title: Locator

  /** The «Subtítulo» field. */
  readonly subtitle: Locator

  /** The multiple select of the event categories. */
  readonly categories: Locator

  /** The multiple select of the terms documents linked to the event. */
  readonly terms: Locator

  /** The day the event starts. */
  readonly startsOn: Locator

  /** The last day of the event. */
  readonly endsOn: Locator

  /** The moment members and sponsors may start signing up, which is optional. */
  readonly earlySignupFrom: Locator

  /** The moment everybody may start signing up. */
  readonly signupFrom: Locator

  /** The moment signups close. */
  readonly signupUntil: Locator

  /** The footer button that saves the event. */
  readonly save: Locator

  constructor(
    private readonly page: Page,
    private readonly heading: 'Nuevo evento' | 'Editar evento',
  ) {
    this.root = page.getByRole('dialog', { name: heading })
    this.title = this.root.getByLabel('Título', { exact: true })
    this.subtitle = this.root.getByLabel('Subtítulo', { exact: true })
    this.categories = this.root.getByLabel('Categorías', { exact: true })
    this.terms = this.root.getByLabel('Términos y condiciones (opcional)')
    this.startsOn = this.root.getByLabel('Inicio del evento')
    this.endsOn = this.root.getByLabel('Fin del evento')
    this.earlySignupFrom = this.root.getByLabel('Apertura de inscripción anticipada (opcional)')
    this.signupFrom = this.root.getByLabel('Apertura de inscripción', { exact: true })
    this.signupUntil = this.root.getByLabel('Cierre de inscripción')
    this.save = this.root.getByRole('button', { name: 'Guardar', exact: true })
  }

  /** Adds the category `name` to the event and closes the list of options. */
  async chooseCategory(name: string): Promise<void> {
    await chooseOption(this.categories, name)
    await this.closeOptions()
  }

  /** Creates the category `name` through the «Nueva» button; the event takes it at once. */
  async createCategory(name: string): Promise<void> {
    await this.root.getByRole('button', { name: 'Nueva', exact: true }).click()
    const dialog = this.page.getByRole('dialog', { name: 'Nueva categoría' })
    await dialog.getByLabel('Nombre', { exact: true }).fill(name)
    await dialog.getByRole('button', { name: 'Crear', exact: true }).click()
    await expect(dialog).toBeHidden()
  }

  /** Links the terms document `name` to the event and closes the list of options. */
  async chooseTerms(name: string): Promise<void> {
    await chooseOption(this.terms, name)
    await this.closeOptions()
  }

  /** The «Obligatorio» switch of the linked terms document `name`. */
  termsRequired(name: string): Locator {
    return this.root.locator('.form__terms-row').filter({ hasText: name }).getByRole('switch')
  }

  /** Turns the «Obligatorio» switch of the linked terms document `name` on or off. */
  async setTermsRequired(name: string, required: boolean): Promise<void> {
    await setSwitch(this.termsRequired(name), required)
  }

  /** Replaces the rich text of the «Descripción» editor. */
  async describe(text: string): Promise<void> {
    await writeRichText(this.root, 'Descripción', text)
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
    await this.root.getByRole('heading', { name: this.heading, exact: true }).click()
  }
}
