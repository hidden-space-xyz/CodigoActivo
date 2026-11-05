import { registerUnverified } from '../../support/api/accounts'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { LoginPage, signInThroughPages, TwoFactorPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'

test.describe('login', () => {
  test('a person signs in with the password and the emailed code', async ({
    page,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'login' })
    const login = new LoginPage(page)
    await login.goto()

    await signInThroughPages(page, mailbox, account)

    const header = new SiteHeader(page)
    await expect(page).toHaveURL('/')
    await expect(header.greeting(account.firstName)).toBeVisible()
    await expect(header.account).toBeVisible()
    await expect(header.login).toBeHidden()
    await expect(header.admin).toBeHidden()
  })

  test('a protected page sends the visitor to log in and back', async ({
    page,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'redirect' })

    await page.goto('/account')
    await expect(page).toHaveURL(/\/login\?redirect=\/account$/)
    await signInThroughPages(page, mailbox, account)

    await expect(page).toHaveURL('/account')
    await expect(page.getByTestId('page-account')).toBeVisible()
  })

  test('a wrong password is refused without telling whether the account exists', async ({
    page,
    accounts,
  }) => {
    const account = await accounts.create({ label: 'wrong-password' })
    const login = new LoginPage(page)
    await login.goto()

    await login.submitPassword({ email: account.email, password: `${PASSWORD}-wrong` })

    await expect(login.error).toHaveText(t('pages.login.error'))
    await expect(page).toHaveURL('/login')
  })

  test('an account whose email is not verified cannot sign in', async ({ page, anonymousApi }) => {
    const { person } = await registerUnverified(anonymousApi, { label: 'unverified' })
    const login = new LoginPage(page)
    await login.goto()

    await login.submitPassword({ email: person.email, password: PASSWORD })

    await expect(login.error).toHaveText(t('errors.UserAccountPendingVerification'))
  })

  test('a wrong code keeps the person on the second step', async ({ page, accounts }) => {
    const account = await accounts.create({ label: 'wrong-code' })
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)

    const twoFactor = new TwoFactorPage(page)
    await expect(twoFactor.heading).toBeVisible()
    await expect(page.getByTestId('login-two-factor-intro')).toContainText(
      t('pages.loginTwoFactor.emailIntroBefore'),
    )
    await twoFactor.enter('000000')

    await expect(twoFactor.error).toHaveText(t('errors.TwoFactorCodeInvalid'))
    await expect(page).toHaveURL('/login/verify')
  })

  test('signing out ends the session', async ({ page, accounts, mailbox }) => {
    const account = await accounts.create({ label: 'logout' })
    const login = new LoginPage(page)
    await login.goto()
    await signInThroughPages(page, mailbox, account)
    const header = new SiteHeader(page)

    await header.logout.click()

    await expect(page).toHaveURL('/login')
    await expect(header.login).toBeVisible()
    await page.goto('/account')
    await expect(page).toHaveURL(/\/login\?redirect=/)
  })
})
