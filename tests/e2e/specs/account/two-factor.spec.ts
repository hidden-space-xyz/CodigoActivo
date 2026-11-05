import { currentUser } from '../../support/api/account'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AccountPage } from '../../support/pages/account/account-page'
import {
  AuthenticatorSetupDialog,
  DisableAuthenticatorDialog,
  TwoFactorSection,
} from '../../support/pages/account/two-factor-section'
import { LoginPage, TwoFactorPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { notification } from '../../support/ui/element-plus'

const INVALID_CODE = t('errors.TwoFactorCodeInvalid')

test.describe('two-step verification', () => {
  test('a person uses the emailed code until they choose an application', async ({
    pageAs,
    member,
  }) => {
    const account = new AccountPage(await pageAs(member))
    await account.goto()
    const twoFactor = new TwoFactorSection(account)

    await expect(twoFactor.method).toHaveText(t('pages.account.twoFactor.methods.Email'))
    await expect(twoFactor.useAuthenticator).toBeVisible()
    await expect(twoFactor.useEmail).toBeHidden()
  })

  test('the setup refuses a wrong password and a wrong first code', async ({
    accounts,
    pageAs,
  }) => {
    const actor = await accounts.actor({ label: 'totp-refused' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const twoFactor = new TwoFactorSection(account)
    await twoFactor.useAuthenticator.click()
    const setup = new AuthenticatorSetupDialog(page)

    await setup.continue.click()
    await expect(setup.root.getByTestId('two-factor-setup-password-error')).toHaveText(
      t('pages.account.twoFactor.form.problems.passwordRequired'),
    )
    await setup.password.fill(`${PASSWORD}-wrong`)
    await setup.continue.click()
    await expect(setup.error).toHaveText(t('errors.UserCurrentPasswordIncorrect'))
    await expect(setup.sharedKey).toBeHidden()

    await setup.unlock(PASSWORD)
    await setup.activate.click()
    await expect(setup.root.getByTestId('two-factor-setup-code-error')).toHaveText(
      t('pages.account.twoFactor.form.problems.codeRequired'),
    )
    await setup.code.fill('000000')
    await setup.activate.click()
    await expect(setup.error).toHaveText(INVALID_CODE)

    await setup.root.getByTestId('two-factor-setup-cancel').click()
    await expect(setup.root).toBeHidden()
    await expect(twoFactor.method).toHaveText(t('pages.account.twoFactor.methods.Email'))
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Email')
  })

  test('an authenticator application replaces the emailed code and can be dropped', async ({
    accounts,
    pageAs,
    page,
  }) => {
    test.slow()
    const actor = await accounts.actor({ label: 'totp' })
    const accountPage = await pageAs(actor)
    const account = new AccountPage(accountPage)
    await account.goto()
    const twoFactor = new TwoFactorSection(account)

    await twoFactor.useAuthenticator.click()
    const setup = new AuthenticatorSetupDialog(accountPage)
    const app = await setup.unlock(PASSWORD)
    await setup.code.fill(await app.nextCode())
    await setup.activate.click()

    await expect(
      notification(accountPage, t('pages.account.twoFactor.enabledSummary')),
    ).toBeVisible()
    await expect(setup.root).toBeHidden()
    await expect(twoFactor.method).toHaveText(t('pages.account.twoFactor.methods.Authenticator'))
    await expect(twoFactor.useEmail).toBeVisible()
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Authenticator')

    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(actor.account)
    const verify = new TwoFactorPage(page)
    await expect(verify.heading).toBeVisible()
    await expect(page.getByTestId('login-two-factor-intro')).toHaveText(
      t('pages.loginTwoFactor.authenticatorIntro'),
    )
    await verify.enter(app.lastCode())
    await expect(verify.error).toHaveText(INVALID_CODE)
    await verify.enter(await app.nextCode())
    await expect(page).not.toHaveURL(/\/login/)
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()

    await twoFactor.useEmail.click()
    const disable = new DisableAuthenticatorDialog(accountPage)
    await disable.submit.click()
    await expect(disable.root.getByTestId('two-factor-disable-password-error')).toHaveText(
      t('pages.account.twoFactor.form.problems.passwordRequired'),
    )
    await expect(disable.root.getByTestId('two-factor-disable-code-error')).toHaveText(
      t('pages.account.twoFactor.form.problems.codeRequired'),
    )
    await disable.password.fill(PASSWORD)
    await disable.code.fill('000000')
    await disable.submit.click()
    await expect(disable.error).toHaveText(INVALID_CODE)
    await disable.code.fill(await app.nextCode())
    await disable.submit.click()

    await expect(
      notification(accountPage, t('pages.account.twoFactor.disabledSummary')),
    ).toBeVisible()
    await expect(disable.root).toBeHidden()
    await expect(twoFactor.method).toHaveText(t('pages.account.twoFactor.methods.Email'))
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Email')
  })
})
