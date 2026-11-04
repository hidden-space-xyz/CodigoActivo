import { readFile } from 'node:fs/promises'
import { expect, type Locator, type Page } from '@playwright/test'

/** A tab of the account page. */
export type AccountTab = 'Perfil' | 'Historial' | 'Diplomas'

/** A titled section of the profile tab. */
export type AccountSection =
  | 'Verificación en dos pasos'
  | 'Menores a mi cargo'
  | 'Consentimientos'
  | 'Tus datos'
  | 'Eliminar mi cuenta'

/** The part of the downloaded personal data file the tests read. */
export interface PersonalDataCopy {
  readonly cuenta: {
    readonly nombre: string
    readonly apellidos: string
    readonly email: string | null
  }
  readonly menores: readonly {
    readonly nombre: string
    readonly apellidos: string
    readonly fechaDeNacimiento: string | null
  }[]
}

/** A file the browser saved, with the name the page gave it and its bytes. */
export interface SavedFile {
  readonly name: string
  readonly content: Buffer
}

const TAB_QUERIES: Readonly<Record<AccountTab, string>> = {
  Perfil: '',
  Historial: '?tab=history',
  Diplomas: '?tab=certificates',
}

/**
 * «Mi cuenta», at `/account`: the heading, the tabs kept in `?tab=`, the profile data list and the
 * sections of the profile tab, each found by its `h2` since they have no accessible name.
 */
export class AccountPage {
  /** The `h1` «Mi área de usuario». */
  readonly heading: Locator

  /** The section with the personal data and the buttons that edit them. */
  readonly profile: Locator

  /** The button that opens «Editar mis datos». */
  readonly editData: Locator

  /** The button that opens «Cambiar contraseña». */
  readonly changePassword: Locator

  constructor(private readonly page: Page) {
    this.heading = page.getByRole('heading', { level: 1, name: 'Mi área de usuario' })
    this.editData = page.getByRole('button', { name: 'Editar datos', exact: true })
    this.changePassword = page.getByRole('button', { name: 'Cambiar contraseña', exact: true })
    this.profile = page.locator('section.acc-pane').filter({ has: this.editData })
  }

  /** Opens the account page on `tab` and waits until that tab is the selected one. */
  async goto(tab: AccountTab = 'Perfil'): Promise<void> {
    await this.page.goto(`/account${TAB_QUERIES[tab]}`)
    await expect(this.heading).toBeVisible()
    await expect(this.tab(tab)).toHaveAttribute('aria-selected', 'true')
  }

  /** The tab named `name`. */
  tab(name: AccountTab): Locator {
    return this.page.getByRole('tab', { name, exact: true })
  }

  /** The section of the profile tab titled `title`. */
  section(title: AccountSection): Locator {
    return this.page
      .locator('section.acc-pane')
      .filter({ has: this.page.getByRole('heading', { level: 2, name: title, exact: true }) })
  }

  /** The value the profile data list shows for the term `label`, such as «Teléfono». */
  profileValue(label: string): Locator {
    return this.profile
      .locator('.acc-info__row')
      .filter({ has: this.page.locator('dt').getByText(label, { exact: true }) })
      .locator('dd')
  }

  /** Clicks «Descargar mis datos» and reads the JSON copy the browser saves. */
  async downloadPersonalData(): Promise<{ name: string; data: PersonalDataCopy }> {
    const button = this.section('Tus datos').getByRole('button', { name: 'Descargar mis datos' })
    const file = await saveDownload(this.page, () => button.click())
    return { name: file.name, data: JSON.parse(file.content.toString('utf8')) as PersonalDataCopy }
  }
}

/** Runs `trigger` and returns the file the page downloads because of it. */
export async function saveDownload(page: Page, trigger: () => Promise<void>): Promise<SavedFile> {
  const [download] = await Promise.all([page.waitForEvent('download'), trigger()])
  const content = await readFile(await download.path())
  return { name: download.suggestedFilename(), content }
}
