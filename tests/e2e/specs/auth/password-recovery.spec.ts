import { expect, test } from '../../support/fixtures/test'
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
    await page.getByRole('link', { name: '¿Has olvidado tu contraseña?' }).click()
    await expect(page.getByRole('heading', { name: '¿Has olvidado tu contraseña?' })).toBeVisible()
    const sentAfter = new Date()

    await page.getByLabel('Correo electrónico').fill(account.email)
    await page.getByRole('button', { name: 'Enviarme un enlace' }).click()

    await expect(page.getByText('Revisa tu correo')).toBeVisible()
    const email = await mailbox.waitFor({
      to: account.email,
      subject: SUBJECTS.passwordReset,
      sentAfter,
    })
    await page.goto(linkIn(email, '/reset-password'))
    await page.getByLabel('Nueva contraseña').fill(NEW_PASSWORD)
    await page.getByLabel('Repite la contraseña').fill(NEW_PASSWORD)
    await page.getByRole('button', { name: 'Cambiar contraseña' }).click()
    await expect(page.getByText('¡Contraseña actualizada!')).toBeVisible()

    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toContainText('No hemos podido iniciar sesión')
    await signInThroughPages(page, mailbox, { email: account.email, password: NEW_PASSWORD })
    await expect(page).toHaveURL('/')
  })

  test('an unknown address gets the same answer as a registered one', async ({ page }) => {
    await page.goto('/forgot-password')

    await page.getByLabel('Correo electrónico').fill('nadie@e2e.codigoactivo.test')
    await page.getByRole('button', { name: 'Enviarme un enlace' }).click()

    await expect(page.getByText('Revisa tu correo')).toBeVisible()
  })

  test('an incomplete reset link asks for a new one', async ({ page }) => {
    await page.goto('/reset-password')

    await expect(page.getByText('El enlace no es válido')).toBeVisible()
    await page.getByRole('link', { name: 'Solicitar un enlace nuevo' }).click()
    await expect(page).toHaveURL('/forgot-password')
  })
})
