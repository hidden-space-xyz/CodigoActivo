import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { PasswordDialog } from '../../support/pages/account/profile-dialogs'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { notification } from '../../support/ui/element-plus'

const NEW_PASSWORD = 'Otra-clave-e2e-2026'

test.describe('password change', () => {
  test('the dialog checks the new password before sending it', async ({ pageAs, participant }) => {
    const page = await pageAs(participant)
    const account = new AccountPage(page)
    await account.goto()
    await account.changePassword.click()
    const dialog = new PasswordDialog(page)

    await dialog.submit('Una-clave-cualquiera', 'corta')
    await expect(dialog.error).toHaveText('La nueva contraseña debe tener al menos 12 caracteres.')

    await dialog.submit(NEW_PASSWORD, NEW_PASSWORD)
    await expect(dialog.error).toHaveText('La nueva contraseña debe ser distinta de la actual.')

    await dialog.submit('Una-clave-cualquiera', NEW_PASSWORD, `${NEW_PASSWORD}-otra`)
    await expect(dialog.error).toHaveText('Las contraseñas no coinciden.')
    await expect(dialog.root).toBeVisible()
  })

  test('a new password closes the session and replaces the old one', async ({
    accounts,
    pageAs,
    mailbox,
  }) => {
    const actor = await accounts.actor({ label: 'password-change' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    await account.changePassword.click()
    const dialog = new PasswordDialog(page)

    await dialog.submit(`${PASSWORD}-wrong`, NEW_PASSWORD)
    await expect(dialog.error).toHaveText('La contraseña actual no es correcta.')

    await dialog.current.fill(PASSWORD)
    await dialog.save.click()

    const login = new LoginPage(page)
    await expect(page).toHaveURL('/login')
    await expect(notification(page, 'Contraseña cambiada')).toBeVisible()
    await expect(login.heading).toBeVisible()
    await expect(new SiteHeader(page).login).toBeVisible()

    await login.submitPassword({ email: actor.account.email, password: PASSWORD })
    await expect(login.error).toContainText('No hemos podido iniciar sesión')

    await signInThroughPages(page, mailbox, { email: actor.account.email, password: NEW_PASSWORD })
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()
  })
})
