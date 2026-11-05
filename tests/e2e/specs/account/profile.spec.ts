import { currentUser } from '../../support/api/account'
import { PASSWORD } from '../../support/config/environment'
import { emailAddress, phoneNumber, uniqueLastName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
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

/** How the API names the status of an active account, which comes from its catalog. */
const ACTIVE_STATUS = 'Activo'

test.describe('profile', () => {
  test('the profile lists the personal data and the news preference', async ({
    pageAs,
    member,
    participant,
  }) => {
    const person = member.account
    const memberAccount = new AccountPage(await pageAs(member))
    await memberAccount.goto()

    await expect(memberAccount.profileValue('name')).toHaveText(
      `${person.firstName} ${person.lastName}`,
    )
    await expect(memberAccount.profileValue('status')).toHaveText(ACTIVE_STATUS)
    await expect(memberAccount.profileValue('national-id')).toHaveText(person.nationalId)
    await expect(memberAccount.profileValue('gender')).toHaveText(GENDER_NAMES[person.gender])
    await expect(memberAccount.profileValue('phone')).toHaveText(person.phone)
    await expect(memberAccount.profileValue('email')).toHaveText(person.email)
    await expect(memberAccount.profileValue('news-consent')).toHaveText(t('common.yes'))

    const participantAccount = new AccountPage(await pageAs(participant))
    await participantAccount.goto()
    await expect(participantAccount.profileValue('email')).toHaveText(participant.account.email)
    await expect(participantAccount.profileValue('news-consent')).toHaveText(t('common.no'))
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
    await chooseOption(dialog.gender, gender)
    await setChecked(dialog.newsConsent, true)
    await expect(dialog.currentPassword).toBeHidden()
    await dialog.save.click()

    await expect(notification(page, t('pages.account.profile.savedSummary'))).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await expect(account.profileValue('name')).toHaveText(`Valeria ${lastName}`)
    await expect(account.profileValue('gender')).toHaveText(GENDER_NAMES[gender])
    await expect(account.profileValue('news-consent')).toHaveText(t('common.yes'))
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
    await expect(dialog.currentPasswordError).toHaveText(
      t('pages.account.profile.contactChange.passwordRequired'),
    )

    await dialog.currentPassword.fill(`${PASSWORD}-wrong`)
    await dialog.save.click()
    await expect(dialog.currentPasswordError).toHaveText(t('errors.UserCurrentPasswordIncorrect'))
    await expect(account.profileValue('phone')).toHaveText(actor.account.phone)

    await dialog.currentPassword.fill(PASSWORD)
    await dialog.save.click()
    await expect(notification(page, t('pages.account.profile.savedSummary'))).toBeVisible()
    await expect(account.profileValue('phone')).toHaveText(phone)
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
    await expect(dialog.nationalIdError).toHaveText(t('validation.nationalIdFormat'))

    await dialog.nationalId.fill(mismatchedNationalId())
    await dialog.nationalId.blur()
    await expect(dialog.nationalIdError).toHaveText(t('validation.nationalIdLetter'))
    await dialog.save.click()
    await expect(dialog.root).toBeVisible()
    await expect(dialog.nationalId).toHaveAttribute('aria-invalid', 'true')

    await dialog.cancel.click()
    await expect(dialog.root).toBeHidden()
    await expect(account.profileValue('national-id')).toHaveText(participant.account.nationalId)
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

    await expect(
      notification(accountPage, t('pages.account.profile.emailChangeSentSummary')),
    ).toBeVisible()
    await expect(account.profileValue('email')).toHaveText(actor.account.email)
    expect((await currentUser(actor.api)).email).toBe(actor.account.email)

    const email = await mailbox.waitFor({ to: newEmail, subject: SUBJECTS.emailChange, sentAfter })
    await accountPage.goto(linkIn(email, '/confirm-email'))
    await expect(accountPage.getByTestId('confirm-email-success')).toBeVisible()
    await accountPage.getByTestId('confirm-email-account').click()
    await expect(account.profileValue('email')).toHaveText(newEmail)
    expect((await currentUser(actor.api)).email).toBe(newEmail)

    await new LoginPage(page).goto()
    await signInThroughPages(page, mailbox, { email: newEmail, password: PASSWORD })
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()
  })

  test('a broken or incomplete email confirmation link is refused', async ({ page }) => {
    const error = page.getByTestId('confirm-email-error')

    await page.goto(
      '/confirm-email#userId=00000000-0000-4000-8000-000000000000&code=not-a-real-code',
    )
    await expect(error).toBeVisible()
    await expect(error).toContainText(t('pages.confirmEmail.hint'))

    await page.goto('/confirm-email')
    await expect(error).toBeVisible()
    await expect(error).toContainText(t('pages.confirmEmail.incompleteLink'))
  })
})
