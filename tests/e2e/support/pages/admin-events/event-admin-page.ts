import { expect, type Locator, type Page } from '@playwright/test'

import type { RoleName, SignupStatusName } from '../../api/catalogs'
import { DataTable } from '../../ui/data-table'
import { chooseOption } from '../../ui/element-plus'

/** A tab of the event admin page. */
type EventAdminTab = 'Actividades' | 'Asistentes' | 'Opiniones' | 'Términos'

/** How the interface names each activity role. */
const ROLE_LABELS: Readonly<Record<RoleName, string>> = {
  leader: 'Líder',
  volunteer: 'Voluntario',
  participant: 'Participante',
}

/** How the interface names each signup status. */
const STATUS_LABELS: Readonly<Record<SignupStatusName, string>> = {
  requested: 'Solicitada',
  confirmed: 'Confirmada',
  denied: 'Rechazada',
}

/**
 * The admin page of one event, at `/admin/events/{id}`: its header with the print buttons, the
 * summary cards and the activities, attendees, opinions and terms tabs.
 */
export class EventAdminPage {
  /** The main landmark of the page. */
  readonly main: Locator

  /** The table of the «Actividades» tab. */
  readonly activities: DataTable

  /** The button of the activities table that opens the dialog to create one. */
  readonly newActivity: Locator

  /** The header button that opens the badges printout. */
  readonly printBadges: Locator

  /** The header button that opens the attendance roster printout. */
  readonly printRoster: Locator

  constructor(private readonly page: Page) {
    this.main = page.getByRole('main')
    this.activities = new DataTable(page, this.panel('Actividades'))
    this.newActivity = this.main.getByRole('button', { name: 'Nueva actividad' })
    this.printBadges = this.main.getByRole('button', { name: 'Imprimir etiquetas' })
    this.printRoster = this.main.getByRole('button', { name: 'Imprimir listados' })
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
    return this.main.getByRole('heading', { level: 1, name: title, exact: true })
  }

  /** The tab `name`. */
  tab(name: EventAdminTab): Locator {
    return this.page.getByRole('tab', { name, exact: true })
  }

  /** The panel of the tab `name`, visible while that tab is selected. */
  panel(name: EventAdminTab): Locator {
    return this.page.getByRole('tabpanel', { name })
  }

  /** Selects the tab `name` and waits for its panel. */
  async openTab(name: EventAdminTab): Promise<void> {
    await this.tab(name).click()
    await expect(this.panel(name)).toBeVisible()
  }

  /** The summary card whose caption is exactly `label`, such as «Actividades» or a role. */
  summaryCard(label: string): Locator {
    return this.main
      .locator('.summary__card')
      .filter({ has: this.page.locator('.summary__label').getByText(label, { exact: true }) })
  }

  /** The figure shown in the summary card captioned `label`. */
  summaryValue(label: string): Locator {
    return this.summaryCard(label).locator('.summary__value')
  }
}

/** The dialog that creates («Nueva actividad») or edits («Editar actividad») an activity. */
export class ActivityFormDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The «Título» field. */
  readonly title: Locator

  /** The «Descripción» textarea. */
  readonly description: Locator

  /** The «Modalidad» select, on site or online. */
  readonly modality: Locator

  /** The «Ubicación o plataforma» field. */
  readonly location: Locator

  /** The moment the activity starts. */
  readonly startsAt: Locator

  /** The moment the activity ends. */
  readonly endsAt: Locator

  /** The footer button that saves the activity. */
  readonly save: Locator

  constructor(page: Page, heading: 'Nueva actividad' | 'Editar actividad') {
    this.root = page.getByRole('dialog', { name: heading })
    this.title = this.root.getByLabel('Título', { exact: true })
    this.description = this.root.getByLabel('Descripción', { exact: true })
    this.modality = this.root.getByLabel('Modalidad', { exact: true })
    this.location = this.root.getByLabel('Ubicación o plataforma')
    this.startsAt = this.root.getByLabel('Inicio', { exact: true })
    this.endsAt = this.root.getByLabel('Fin', { exact: true })
    this.save = this.root.getByRole('button', { name: 'Guardar', exact: true })
  }

  /** The spin button with the number of people wanted in `role`. */
  capacity(role: RoleName): Locator {
    return this.root.getByRole('spinbutton', { name: ROLE_LABELS[role], exact: true })
  }

  /** Picks the modality `name`, «Presencial» or «Online». */
  async chooseModality(name: 'Presencial' | 'Online'): Promise<void> {
    await chooseOption(this.modality, name)
  }
}

/** The «Asistentes» tab: everyone signed up for the event, with their signups and contact. */
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
    this.root = page.getByRole('tabpanel', { name: 'Asistentes' })
    this.search = this.root.getByPlaceholder('Buscar por nombre, correo o teléfonos')
    this.count = this.root.getByText(/^\d+ asistentes?$/)
    this.sendEmail = this.root.getByRole('button', { name: 'Enviar correo', exact: true })
  }

  /** The entry of the attendee whose full name is exactly `name`. */
  attendee(name: string): Locator {
    return this.root
      .locator('li.attendee')
      .filter({ has: this.page.getByText(name, { exact: true }) })
  }

  /** The signup of the attendee `name` for the activity titled `activity`. */
  signup(name: string, activity: string): Locator {
    return this.attendee(name).locator('li.assignment').filter({ hasText: activity })
  }

  /** Opens «Cambiar estado» on a signup, picks `status` and applies it. */
  async changeStatus(name: string, activity: string, status: SignupStatusName): Promise<void> {
    await this.change(name, activity, 'Cambiar estado', 'Estado', STATUS_LABELS[status])
  }

  /** Opens «Cambiar rol» on a signup, picks `role` and applies it. */
  async changeRole(name: string, activity: string, role: RoleName): Promise<void> {
    await this.change(name, activity, 'Cambiar rol', 'Rol', ROLE_LABELS[role])
  }

  private async change(
    name: string,
    activity: string,
    action: string,
    label: string,
    option: string,
  ): Promise<void> {
    await this.signup(name, activity).getByRole('button', { name: action, exact: true }).click()
    const dialog = this.page.getByRole('dialog', { name: action })
    await expect(dialog.getByText(`${name} · ${activity}`)).toBeVisible()
    await chooseOption(dialog.getByLabel(label, { exact: true }), option)
    await dialog.getByRole('button', { name: 'Aplicar', exact: true }).click()
  }
}

/** The «Opiniones» tab: the ratings attendees left, without their authors. */
export class OpinionsTab {
  /** The panel of the tab. */
  readonly root: Locator

  /** The ratings listed. */
  readonly ratings: Locator

  constructor(page: Page) {
    this.root = page.getByRole('tabpanel', { name: 'Opiniones' })
    this.ratings = this.root.getByRole('listitem')
  }
}

/** The «Términos» tab: the decision taken for each person on each terms document. */
export class TermsTab {
  /** The panel of the tab. */
  readonly root: Locator

  /** The table of decisions. */
  readonly table: DataTable

  constructor(page: Page) {
    this.root = page.getByRole('tabpanel', { name: 'Términos' })
    this.table = new DataTable(page, this.root)
  }

  /** The row of the decision of the person `name` on the document `document`. */
  decision(name: string, document: string): Locator {
    return this.table.row(name).filter({ hasText: document })
  }
}
