import { currentUser } from '../../support/api/account'
import { PASSWORD } from '../../support/config/environment'
import { emailAddress, phoneNumber, uniqueLastName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { linkIn, SUBJECTS } from '../../support/mail/mailbox'
import { AccountPage } from '../../support/pages/account/account-page'
import {
  mismatchedNationalId,
  otherGender,
  ProfileDialog,
} from '../../support/pages/account/profile-dialogs'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'
import { GENDER_NAMES } from '../../support/pages/registration-page'
import { SiteHeader } from '../../support/pages/site-header'
import { chooseOption, notification, setChecked } from '../../support/ui/element-plus'

test.describe('profile', () => {
  test('the profile lists the personal data and the news preference', async ({
    pageAs,
    member,
    participant,
  }) => {
    const person = member.account
    const memberAccount = new AccountPage(await pageAs(member))
    await memberAccount.goto()

    await expect(memberAccount.profileValue('Nombre')).toHaveText(
      `${person.firstName} ${person.lastName}`,
    )
    await expect(memberAccount.profileValue('Estado')).toHaveText('Activo')
    await expect(memberAccount.profileValue('DNI/NIE')).toHaveText(person.nationalId)
    await expect(memberAccount.profileValue('Género')).toHaveText(GENDER_NAMES[person.gender])
    await expect(memberAccount.profileValue('Teléfono')).toHaveText(person.phone)
    await expect(memberAccount.profileValue('Correo')).toHaveText(person.email)
    await expect(memberAccount.profileValue('Acepta novedades')).toHaveText('Sí')

    const participantAccount = new AccountPage(await pageAs(participant))
    await participantAccount.goto()
    await expect(participantAccount.profileValue('Correo')).toHaveText(participant.account.email)
    await expect(participantAccount.profileValue('Acepta novedades')).toHaveText('No')
  })

  test('changing the name, the gender and the news preference needs no password', async ({
    accounts,
    pageAs,
  }) => {
    const actor = await accounts.actor({ label: 'profile-edit' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    await account.editData.click()
    const dialog = new ProfileDialog(page)
    const lastName = uniqueLastName()
    const gender = otherGender(actor.account.gender)

    await dialog.firstName.fill('Valeria')
    await dialog.lastName.fill(lastName)
    await chooseOption(dialog.gender, GENDER_NAMES[gender])
    await setChecked(dialog.newsConsent, true)
    await expect(dialog.currentPassword).toBeHidden()
    await dialog.save.click()

    await expect(notification(page, 'Datos actualizados')).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await expect(account.profileValue('Nombre')).toHaveText(`Valeria ${lastName}`)
    await expect(account.profileValue('Género')).toHaveText(GENDER_NAMES[gender])
    await expect(account.profileValue('Acepta novedades')).toHaveText('Sí')
    await expect(new SiteHeader(page).greeting('Valeria')).toBeVisible()
    expect(await currentUser(actor.api)).toMatchObject({
      firstName: 'Valeria',
      lastName,
      gender,
      newsConsent: true,
      email: actor.account.email,
    })
  })

  test('changing the phone asks for the current password and refuses a wrong one', async ({
    accounts,
    pageAs,
  }) => {
    const actor = await accounts.actor({ label: 'profile-phone' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    await account.editData.click()
    const dialog = new ProfileDialog(page)
    const phone = phoneNumber()

    await expect(dialog.currentPassword).toBeHidden()
    await dialog.phone.fill(phone)
    await expect(dialog.currentPassword).toBeVisible()
    await dialog.save.click()
    await expect(dialog.root.getByText('Introduce tu contraseña actual.')).toBeVisible()

    await dialog.currentPassword.fill(`${PASSWORD}-wrong`)
    await dialog.save.click()
    await expect(dialog.root.getByText('La contraseña actual no es correcta.')).toBeVisible()
    await expect(account.profileValue('Teléfono')).toHaveText(actor.account.phone)

    await dialog.currentPassword.fill(PASSWORD)
    await dialog.save.click()
    await expect(notification(page, 'Datos actualizados')).toBeVisible()
    await expect(account.profileValue('Teléfono')).toHaveText(phone)
    expect((await currentUser(actor.api)).phone).toBe(phone)
  })

  test('the DNI/NIE must have its shape and a matching control letter', async ({
    pageAs,
    participant,
  }) => {
    const page = await pageAs(participant)
    const account = new AccountPage(page)
    await account.goto()
    await account.editData.click()
    const dialog = new ProfileDialog(page)

    await dialog.nationalId.fill('1234')
    await dialog.nationalId.blur()
    await expect(
      dialog.root.getByText(
        'Escribe 8 números y la letra (DNI) o X, Y o Z, 7 números y la letra (NIE).',
      ),
    ).toBeVisible()

    await dialog.nationalId.fill(mismatchedNationalId())
    await dialog.nationalId.blur()
    await expect(
      dialog.root.getByText('El número y la letra no concuerdan. Revisa que estén bien escritos.'),
    ).toBeVisible()
    await dialog.save.click()
    await expect(dialog.root).toBeVisible()
    await expect(dialog.nationalId).toHaveAttribute('aria-invalid', 'true')

    await dialog.cancel.click()
    await expect(dialog.root).toBeHidden()
    await expect(account.profileValue('DNI/NIE')).toHaveText(participant.account.nationalId)
  })

  test('a new email replaces the old one only once its link is opened', async ({
    accounts,
    pageAs,
    page,
    mailbox,
  }) => {
    const actor = await accounts.actor({ label: 'email-change' })
    const accountPage = await pageAs(actor)
    const account = new AccountPage(accountPage)
    await account.goto()
    await account.editData.click()
    const dialog = new ProfileDialog(accountPage)
    const newEmail = emailAddress('new-email')

    await dialog.email.fill(newEmail)
    await dialog.currentPassword.fill(PASSWORD)
    const sentAfter = new Date()
    await dialog.save.click()

    await expect(notification(accountPage, 'Revisa tu nuevo correo')).toBeVisible()
    await expect(account.profileValue('Correo')).toHaveText(actor.account.email)
    expect((await currentUser(actor.api)).email).toBe(actor.account.email)

    const email = await mailbox.waitFor({ to: newEmail, subject: SUBJECTS.emailChange, sentAfter })
    await accountPage.goto(linkIn(email, '/confirm-email'))
    await expect(accountPage.getByRole('heading', { name: '¡Correo actualizado!' })).toBeVisible()
    await accountPage.getByRole('main').getByRole('link', { name: 'Mi cuenta' }).click()
    await expect(account.profileValue('Correo')).toHaveText(newEmail)
    expect((await currentUser(actor.api)).email).toBe(newEmail)

    await new LoginPage(page).goto()
    await signInThroughPages(page, mailbox, { email: newEmail, password: PASSWORD })
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()
  })

  test('a broken or incomplete email confirmation link is refused', async ({ page }) => {
    const errorTitle = page.getByRole('heading', { name: 'No hemos podido confirmar el correo' })

    await page.goto(
      '/confirm-email#userId=00000000-0000-4000-8000-000000000000&code=not-a-real-code',
    )
    await expect(errorTitle).toBeVisible()
    await expect(page.getByText(/vuelve a cambiar el correo desde «Mi cuenta»/)).toBeVisible()

    await page.goto('/confirm-email')
    await expect(errorTitle).toBeVisible()
    await expect(page.getByRole('alert')).toHaveText(
      'El enlace de confirmación no es válido o está incompleto.',
    )
  })
})
