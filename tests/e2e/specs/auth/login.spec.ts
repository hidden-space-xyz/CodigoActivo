import { registerUnverified, resetPassword } from '../../support/api/accounts'
import { lockOutWithWrongPasswords } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { emailAddress } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { codeIn, SUBJECTS } from '../../support/mail/mailbox'
import {
  LoginPage,
  sessionExpiry,
  signInThroughPages,
  TwoFactorPage,
} from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { setChecked } from '../../support/ui/element-plus'

const RESEND_COOLDOWN_MS = 60_000

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

  test('a session ends with the browser unless the person asks to stay signed in', async ({
    page,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'browser-session' })
    await new LoginPage(page).goto()

    await signInThroughPages(page, mailbox, account)

    expect(await sessionExpiry(page)).toBe(-1)
  })

  test('staying signed in keeps the session after the browser closes', async ({
    page,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'keep-signed-in' })
    const login = new LoginPage(page)
    await login.goto()
    await setChecked(login.keepSignedIn, true)

    await signInThroughPages(page, mailbox, account)

    expect(await sessionExpiry(page)).toBeGreaterThan(Date.now() / 1_000)
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

    await login.goto()
    await login.submitPassword({ email: emailAddress('nobody'), password: PASSWORD })

    await expect(login.error).toHaveText(t('pages.login.error'))
  })

  test('an account whose email is not verified cannot sign in', async ({ page, anonymousApi }) => {
    const { person } = await registerUnverified(anonymousApi, { label: 'unverified' })
    const login = new LoginPage(page)
    await login.goto()

    await login.submitPassword({ email: person.email, password: PASSWORD })

    await expect(login.error).toHaveText(t('errors.UserAccountPendingVerification'))
  })

  test('five wrong passwords lock the account and a password reset unlocks it', async ({
    page,
    accounts,
    anonymousApi,
    clientAddress,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'locked' })
    const sentAfter = new Date()

    await lockOutWithWrongPasswords(clientAddress, account.email)

    await mailbox.waitFor({ to: account.email, subject: SUBJECTS.passwordLocked, sentAfter })
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toHaveText(t('pages.login.error'))

    const unlocked = { email: account.email, password: `${PASSWORD}-nueva` }
    await resetPassword(anonymousApi, mailbox, unlocked)
    await login.goto()
    await signInThroughPages(page, mailbox, unlocked)
    await expect(new SiteHeader(page).greeting(account.firstName)).toBeVisible()
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

  test('another emailed code can be asked for once a minute has passed', async ({
    page,
    accounts,
    mailbox,
  }) => {
    test.slow()
    const account = await accounts.create({ label: 'resend' })
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)
    const twoFactor = new TwoFactorPage(page)
    await expect(twoFactor.resend).toBeDisabled()

    await expect(twoFactor.resend).toBeEnabled({ timeout: RESEND_COOLDOWN_MS + 10_000 })
    const sentAfter = new Date()
    await twoFactor.resend.click()

    const email = await mailbox.waitFor({
      to: account.email,
      subject: SUBJECTS.loginCode,
      sentAfter,
    })
    await twoFactor.enter(codeIn(email))
    await expect(new SiteHeader(page).greeting(account.firstName)).toBeVisible()
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
    await expect(header.account).toBeHidden()
    await page.goto('/account')
    await expect(page).toHaveURL(/\/login\?redirect=/)
  })
})
