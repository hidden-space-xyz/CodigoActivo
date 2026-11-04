import { findUser } from '../../support/api/accounts'
import { currentUser } from '../../support/api/account'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { SUBJECTS } from '../../support/mail/mailbox'
import { AccountPage } from '../../support/pages/account/account-page'
import {
  DeleteAccountDialog,
  DeleteAccountSection,
} from '../../support/pages/account/delete-account'
import { LoginPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { notification, setChecked } from '../../support/ui/element-plus'

test.describe('account deletion', () => {
  test('a person deletes their account with the code emailed to them', async ({
    accounts,
    pageAs,
    mailbox,
    initialAdmin,
  }) => {
    const actor = await accounts.actor({ label: 'delete' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    await new DeleteAccountSection(account).open.click()
    const dialog = new DeleteAccountDialog(page)

    const code = await dialog.requestCode(mailbox, actor.account)
    await expect(
      dialog.root.getByText(`Te hemos enviado un código a ${actor.account.email}.`),
    ).toBeVisible()
    await expect(dialog.submit).toBeDisabled()
    await dialog.code.fill(code)
    await expect(dialog.submit).toBeDisabled()
    await setChecked(dialog.irreversible, true)
    await expect(dialog.submit).toBeEnabled()
    await dialog.submit.click()

    await expect(page).toHaveURL('/')
    await expect(notification(page, 'Cuenta eliminada')).toBeVisible()
    await expect(new SiteHeader(page).login).toBeVisible()
    expect(await findUser(initialAdmin.api, actor.account.email)).toBeUndefined()

    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(actor.account)
    await expect(login.error).toContainText('No hemos podido iniciar sesión')
  })

  test('a wrong password or code leaves the account in place', async ({
    accounts,
    pageAs,
    mailbox,
  }) => {
    const actor = await accounts.actor({ label: 'delete-refused' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    await new DeleteAccountSection(account).open.click()
    const dialog = new DeleteAccountDialog(page)

    await dialog.sendCode.click()
    await expect(dialog.root.getByText('Introduce tu contraseña.')).toBeVisible()
    await dialog.password.fill(`${PASSWORD}-wrong`)
    const sentAfter = new Date()
    await dialog.sendCode.click()
    await expect(dialog.error).toHaveText('La contraseña actual no es correcta.')
    await expect(dialog.code).toBeHidden()
    expect(
      await mailbox.received({
        to: actor.account.email,
        subject: SUBJECTS.accountDeletionCode,
        sentAfter,
      }),
    ).toBe(false)

    await dialog.requestCode(mailbox, actor.account)
    await dialog.code.fill('000000')
    await setChecked(dialog.irreversible, true)
    await dialog.submit.click()
    await expect(dialog.error).toHaveText('El código no es válido o ha caducado.')
    await expect(dialog.root).toBeVisible()
    expect((await currentUser(actor.api)).id).toBe(actor.account.id)
  })

  test('the initial administrator cannot delete its account', async ({ pageAs, initialAdmin }) => {
    const account = new AccountPage(await pageAs(initialAdmin))
    await account.goto()
    const deletion = new DeleteAccountSection(account)

    await expect(deletion.initialAdminNote).toBeVisible()
    await expect(deletion.open).toBeHidden()
  })

  test('another administrator deletes their own account', async ({
    accounts,
    pageAs,
    mailbox,
    initialAdmin,
  }) => {
    const actor = await accounts.actor({ label: 'delete-admin', admin: true })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const header = new SiteHeader(page)
    await expect(header.admin).toBeVisible()
    const deletion = new DeleteAccountSection(account)

    await deletion.open.click()
    await expect(deletion.initialAdminNote).toBeHidden()
    await new DeleteAccountDialog(page).deleteWithEmailedCode(mailbox, actor.account)

    await expect(page).toHaveURL('/')
    await expect(notification(page, 'Cuenta eliminada')).toBeVisible()
    await expect(header.admin).toBeHidden()
    expect(await findUser(initialAdmin.api, actor.account.email)).toBeUndefined()
  })
})
