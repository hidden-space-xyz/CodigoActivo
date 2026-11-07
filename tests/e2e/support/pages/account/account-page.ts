import { expect, type Locator, type Page } from '@playwright/test'

import { saveDownload } from '../../ui/download'

/** A tab of the account page, by the name it has in `?tab=`. */
export type AccountTab = 'profile' | 'history' | 'certificates'

/** A section of the profile tab. */
export type AccountSection =
  'two-factor' | 'minors' | 'consents' | 'personal-data' | 'delete-account'

/** A term of the profile data list. */
export type ProfileField =
  | 'name'
  | 'status'
  | 'national-id'
  | 'gender'
  | 'phone'
  | 'secondary-phone'
  | 'email'
  | 'news-consent'

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

const TAB_QUERIES: Readonly<Record<AccountTab, string>> = {
  profile: '',
  history: '?tab=history',
  certificates: '?tab=certificates',
}

/**
 * «Mi cuenta», at `/account`: the tabs kept in `?tab=`, the profile data list and the sections of
 * the profile tab.
 */
export class AccountPage {
  /** The content of the account page, shown only while it is the current page. */
  readonly heading: Locator

  /** The section with the personal data and the buttons that edit them. */
  readonly profile: Locator

  /** The button that opens «Editar mis datos». */
  readonly editData: Locator

  /** The button that opens «Cambiar contraseña». */
  readonly changePassword: Locator

  constructor(private readonly page: Page) {
    this.heading = page.getByTestId('page-account')
    this.profile = page.getByTestId('profile-section')
    this.editData = this.profile.getByTestId('profile-edit-data')
    this.changePassword = this.profile.getByTestId('profile-change-password')
  }

  /** Opens the account page on `tab` and waits until that tab is the selected one. */
  async goto(tab: AccountTab = 'profile'): Promise<void> {
    await this.page.goto(`/account${TAB_QUERIES[tab]}`)
    await expect(this.heading).toBeVisible()
    await expect(this.tab(tab)).toHaveAttribute('aria-selected', 'true')
  }

  /** The header of the tab `name`, which reports in `aria-selected` whether it is the selected one. */
  tab(name: AccountTab): Locator {
    return this.page
      .locator('.el-tabs__item')
      .filter({ has: this.page.getByTestId(`account-tab-${name}`) })
  }

  /** The section `name` of the profile tab. */
  section(name: AccountSection): Locator {
    return this.page.getByTestId(`${name}-section`)
  }

  /** The value the profile data list shows for the term `field`, such as the phone. */
  profileValue(field: ProfileField): Locator {
    return this.profile.getByTestId(`profile-value-${field}`)
  }

  /** Clicks «Descargar mis datos» and reads the JSON copy the browser saves. */
  async downloadPersonalData(): Promise<{ name: string; data: PersonalDataCopy }> {
    const button = this.section('personal-data').getByTestId('personal-data-download')
    const file = await saveDownload(this.page, () => button.click())
    return { name: file.name, data: JSON.parse(file.content.toString('utf8')) as PersonalDataCopy }
  }
}
