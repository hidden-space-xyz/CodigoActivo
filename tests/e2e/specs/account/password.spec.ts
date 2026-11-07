import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { SUBJECTS } from '../../support/mail/mailbox'
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
    await expect(dialog.error).toHaveText(t('validation.newPasswordMin', { min: 12 }))

    await dialog.submit(NEW_PASSWORD, NEW_PASSWORD)
    await expect(dialog.error).toHaveText(t('validation.newPasswordSameAsCurrent'))

    await dialog.submit('Una-clave-cualquiera', NEW_PASSWORD, `${NEW_PASSWORD}-otra`)
    await expect(dialog.error).toHaveText(t('validation.passwordsMismatch'))
    await expect(dialog.root).toBeVisible()
  })

  test('a new password closes the session, warns the person and replaces the old one', async ({
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
    await expect(dialog.error).toHaveText(t('errors.UserCurrentPasswordIncorrect'))

    await dialog.current.fill(PASSWORD)
    const sentAfter = new Date()
    await dialog.save.click()

    const login = new LoginPage(page)
    await expect(page).toHaveURL('/login')
    await expect(
      notification(page, t('pages.account.profile.passwordUpdatedSummary')),
    ).toBeVisible()
    await expect(login.heading).toBeVisible()
    await expect(new SiteHeader(page).login).toBeVisible()
    await mailbox.waitFor({
      to: actor.account.email,
      subject: SUBJECTS.securityAlert,
      sentAfter,
    })

    await login.submitPassword({ email: actor.account.email, password: PASSWORD })
    await expect(login.error).toHaveText(t('pages.login.error'))

    await signInThroughPages(page, mailbox, { email: actor.account.email, password: NEW_PASSWORD })
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()
  })
})
