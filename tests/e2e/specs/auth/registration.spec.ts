import { findUser } from '../../support/api/accounts'
import { PASSWORD } from '../../support/config/environment'
import { newMinor, newPerson } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { linkIn, SUBJECTS } from '../../support/mail/mailbox'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'
import { RegistrationPage } from '../../support/pages/registration-page'

test.describe('registration', () => {
  test('a minor is asked to let a guardian register', async ({ page }) => {
    const registration = new RegistrationPage(page)
    await page.goto('/register')

    await registration.declineAdult.click()

    await expect(
      page.getByRole('heading', { name: 'Necesitas a tu padre, madre o tutor legal' }),
    ).toBeVisible()
    await expect(registration.submit).toBeHidden()
  })

  test('a guardian registers with two minors, verifies the address and signs in', async ({
    page,
    mailbox,
    initialAdmin,
  }) => {
    const person = newPerson('signup')
    const first = newMinor(person, 9)
    const minors = [first, newMinor(person, 12, [first])]
    const registration = new RegistrationPage(page)
    await registration.open()
    await registration.fill({ person, password: PASSWORD, minors, newsConsent: true })
    const sentAfter = new Date()

    await registration.submit.click()

    await expect(registration.success).toBeVisible()
    await expect(page.getByText(person.email)).toBeVisible()
    await expect(page.getByText(/2 menores/)).toBeVisible()
    const email = await mailbox.waitFor({
      to: person.email,
      subject: SUBJECTS.verification,
      sentAfter,
    })
    await page.goto(linkIn(email, '/verify-account'))
    await expect(page.getByRole('heading', { name: '¡Cuenta verificada!' })).toBeVisible()

    await page.getByRole('main').getByRole('link', { name: 'Iniciar sesión' }).click()
    await expect(new LoginPage(page).heading).toBeVisible()
    await signInThroughPages(page, mailbox, { email: person.email, password: PASSWORD })
    await page.goto('/account')
    for (const minor of minors) {
      await expect(page.getByText(`${minor.firstName} ${minor.lastName}`)).toBeVisible()
    }
    const user = await findUser(initialAdmin.api, person.email)
    expect(user?.newsConsent).toBe(true)
    expect(user?.dependentCount).toBe(2)
  })

  test('the form refuses a short password and mismatching confirmation', async ({ page }) => {
    const registration = new RegistrationPage(page)
    await registration.open()
    await registration.fill({
      person: newPerson('invalid'),
      password: 'corta',
      confirmPassword: 'otra',
    })

    await registration.submit.click()

    const access = registration.section('Acceso a tu cuenta')
    await expect(access.getByText('La contraseña debe tener al menos 12 caracteres.')).toBeVisible()
    await expect(access.getByText('Las contraseñas no coinciden.')).toBeVisible()
    await expect(registration.success).toBeHidden()
  })

  test('a broken verification link is refused', async ({ page }) => {
    await page.goto(
      '/verify-account#userId=00000000-0000-4000-8000-000000000000&code=not-a-real-code',
    )

    await expect(
      page.getByRole('heading', { name: 'No hemos podido verificar tu cuenta' }),
    ).toBeVisible()
  })
})
