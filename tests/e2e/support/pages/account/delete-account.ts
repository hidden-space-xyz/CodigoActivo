import { expect, type Locator, type Page } from '@playwright/test'

import type { Credentials } from '../../api/accounts'
import { codeIn, SUBJECTS, type Mailbox } from '../../mail/mailbox'
import { setChecked } from '../../ui/element-plus'
import type { AccountPage } from './account-page'

/** The section «Eliminar mi cuenta» of the profile tab. */
export class DeleteAccountSection {
  /** The section itself. */
  readonly root: Locator

  /** The button that opens the deletion dialog. */
  readonly open: Locator

  /** The note the initial administrator sees instead of the button. */
  readonly initialAdminNote: Locator

  constructor(account: AccountPage) {
    this.root = account.section('delete-account')
    this.open = this.root.getByTestId('delete-account-open')
    this.initialAdminNote = this.root.getByTestId('delete-account-initial-admin-note')
  }
}

/**
 * The dialog «Eliminar mi cuenta»: the password step sends the emailed code, and the code step
 * deletes the account once the code is typed and the irreversibility accepted.
 */
export class DeleteAccountDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Tu contraseña», in the password step. */
  readonly password: Locator

  /** «Enviar código», which sends the password step. */
  readonly sendCode: Locator

  /** «Código de confirmación», in the code step. */
  readonly code: Locator

  /** The checkbox that accepts that the deletion cannot be undone. */
  readonly irreversible: Locator

  /** «Eliminar definitivamente». */
  readonly submit: Locator

  /** The error the dialog shows for a refused request. */
  readonly error: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('delete-account-dialog')
    this.password = this.root.getByTestId('delete-account-password')
    this.sendCode = this.root.getByTestId('delete-account-send-code')
    this.code = this.root.getByTestId('delete-account-code')
    this.irreversible = this.root.getByTestId('delete-account-irreversible')
    this.submit = this.root.getByTestId('delete-account-submit')
    this.error = this.root.getByTestId('delete-account-error')
  }

  /** Sends the password step and waits for the code it emails to `credentials.email`. */
  async requestCode(mailbox: Mailbox, credentials: Credentials): Promise<string> {
    await this.password.fill(credentials.password)
    const sentAfter = new Date()
    await this.sendCode.click()
    await expect(this.code).toBeVisible()
    const email = await mailbox.waitFor({
      to: credentials.email,
      subject: SUBJECTS.accountDeletionCode,
      sentAfter,
    })
    return codeIn(email)
  }

  /** Deletes the account with the emailed code, accepting that it cannot be undone. */
  async deleteWithEmailedCode(mailbox: Mailbox, credentials: Credentials): Promise<void> {
    await this.code.fill(await this.requestCode(mailbox, credentials))
    await setChecked(this.irreversible, true)
    await this.submit.click()
  }
}
