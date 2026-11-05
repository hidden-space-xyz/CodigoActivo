import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { linkIn, SUBJECTS } from '../../support/mail/mailbox'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'

const NEW_PASSWORD = 'Otra-clave-segura-2026'

test.describe('password recovery', () => {
  test('a person resets a forgotten password from the emailed link', async ({
    page,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'forgot' })
    const login = new LoginPage(page)
    await login.goto()
    await page.getByTestId('login-forgot-password').click()
    await expect(page.getByTestId('page-forgot-password')).toBeVisible()
    const sentAfter = new Date()

    await page.getByTestId('forgot-password-email').fill(account.email)
    await page.getByTestId('forgot-password-submit').click()

    await expect(page.getByTestId('forgot-password-sent')).toBeVisible()
    const email = await mailbox.waitFor({
      to: account.email,
      subject: SUBJECTS.passwordReset,
      sentAfter,
    })
    await page.goto(linkIn(email, '/reset-password'))
    await page.getByTestId('reset-password-new').fill(NEW_PASSWORD)
    await page.getByTestId('reset-password-confirm').fill(NEW_PASSWORD)
    await page.getByTestId('reset-password-submit').click()
    await expect(page.getByTestId('reset-password-success')).toBeVisible()

    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toHaveText(t('pages.login.error'))
    await signInThroughPages(page, mailbox, { email: account.email, password: NEW_PASSWORD })
    await expect(page).toHaveURL('/')
  })

  test('an unknown address gets the same answer as a registered one', async ({ page }) => {
    await page.goto('/forgot-password')

    await page.getByTestId('forgot-password-email').fill('nadie@e2e.codigoactivo.test')
    await page.getByTestId('forgot-password-submit').click()

    await expect(page.getByTestId('forgot-password-sent')).toBeVisible()
  })

  test('an incomplete reset link asks for a new one', async ({ page }) => {
    await page.goto('/reset-password')

    await expect(page.getByTestId('reset-password-invalid')).toBeVisible()
    await page.getByTestId('reset-password-request-link').click()
    await expect(page).toHaveURL('/forgot-password')
  })
})
