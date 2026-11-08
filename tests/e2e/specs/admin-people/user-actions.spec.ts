import { findUser } from '../../support/api/accounts'
import { signupRoleNames } from '../../support/api/admin-people'
import { CATALOG, ROLE_LABELS, USER_TYPE_LABELS } from '../../support/api/catalogs'
import { currentUser, enrollAuthenticator } from '../../support/api/my-account'
import { PASSWORD } from '../../support/config/environment'
import { fullName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { SUBJECTS } from '../../support/mail/mailbox'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'
import { LoginPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { chooseOption, confirm, messageBox, notification } from '../../support/ui/element-plus'

test.describe('admin user actions', () => {
  test('a participant made a member may then lead activities', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'promoted' })
    expect(await signupRoleNames(person.api, person.account.id)).not.toContain(ROLE_LABELS.leader)
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)

    await users.act(person.account, 'changeType')
    const dialog = page.getByTestId('user-type-dialog')
    await chooseOption(dialog.getByTestId('user-type-dialog-type'), CATALOG.userTypes.member)
    await dialog.getByTestId('user-type-dialog-apply').click()

    await expect(notification(page, t('pages.admin.users.toasts.typeUpdated'))).toBeVisible()
    await expect(dialog).toBeHidden()
    await expect(users.row(person.account)).toContainText(USER_TYPE_LABELS.member)
    expect(await signupRoleNames(person.api, person.account.id)).toContain(ROLE_LABELS.leader)
  })

  test('the superadministrator gives a cargo with its password and opens the administration', async ({
    pageAs,
    initialAdmin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'granted', type: 'member' })
    const page = await pageAs(initialAdmin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)

    await users.act(person.account, 'staffRole')
    const dialog = page.getByTestId('staff-role-dialog')
    await chooseOption(dialog.getByTestId('staff-role-dialog-role'), 'Administrator')
    const password = dialog.getByTestId('admin-password-input')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByTestId('admin-password-confirm').click()

    await expect(dialog.getByTestId('admin-password-error')).toHaveText(
      t('errors.UserCurrentPasswordIncorrect'),
    )
    await expect(password).toHaveAttribute('aria-invalid', 'true')

    await password.fill(PASSWORD)
    await dialog.getByTestId('admin-password-confirm').click()

    await expect(notification(page, t('pages.admin.users.toasts.staffRoleChanged'))).toBeVisible()
    await expect(dialog).toBeHidden()
    await expect(users.row(person.account).getByTestId('users-staff-role')).toHaveText(
      t('entities.user.staffRole.Administrator'),
    )
    const personPage = await pageAs(person)
    await personPage.goto('/')
    await expect(new SiteHeader(personPage).admin).toBeVisible()
  })

  test('only a member may get a cargo', async ({ pageAs, initialAdmin, accounts }) => {
    const account = await accounts.create({ label: 'not-member' })
    const page = await pageAs(initialAdmin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(account.lastName)

    await users.act(account, 'staffRole')
    const dialog = page.getByTestId('staff-role-dialog')
    await chooseOption(dialog.getByTestId('staff-role-dialog-role'), 'Editor')
    await dialog.getByTestId('admin-password-input').fill(PASSWORD)
    await dialog.getByTestId('admin-password-confirm').click()

    await expect(notification(page, t('errors.UserStaffRoleRequiresMember'))).toBeVisible()
    await expect(users.row(account).getByTestId('users-staff-role')).not.toHaveText(
      t('entities.user.staffRole.Editor'),
    )
  })

  test('leaving an account without a cargo closes the administration to that person', async ({
    pageAs,
    initialAdmin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'revoked', type: 'member', editor: true })
    const editor = t('entities.user.staffRole.Editor')
    const page = await pageAs(initialAdmin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)
    await expect(users.row(person.account).getByTestId('users-staff-role')).toHaveText(editor)

    await users.act(person.account, 'staffRole')
    const dialog = page.getByTestId('staff-role-dialog')
    await chooseOption(dialog.getByTestId('staff-role-dialog-role'), 'None')
    await expect(dialog.getByTestId('admin-password-input')).toBeHidden()
    await dialog.getByTestId('admin-password-confirm').click()

    await expect(notification(page, t('pages.admin.users.toasts.staffRoleChanged'))).toBeVisible()
    await expect(users.row(person.account).getByTestId('users-staff-role')).not.toHaveText(editor)
    const personPage = await pageAs(person)
    await personPage.goto('/admin/events')
    await expect(personPage).toHaveURL('/')
    await expect(new SiteHeader(personPage).admin).toBeHidden()
  })

  test('another administrator neither changes cargos nor deletes accounts', async ({
    pageAs,
    admin,
    member,
  }) => {
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.find(member.account.lastName)

    await users.openMenu(member.account)

    await expect(users.menuItem('changeType')).toBeVisible()
    await expect(users.menuItem('staffRole')).toHaveCount(0)
    await expect(users.menuItem('privacyOfficer')).toHaveCount(0)
    await expect(users.menuItem('delete')).toHaveCount(0)
  })

  test('an administrator confirms with a password that a lost authenticator is removed', async ({
    pageAs,
    admin,
    accounts,
    mailbox,
  }) => {
    const person = await accounts.actor({ label: 'reset-2fa' })
    await enrollAuthenticator(person.api, mailbox, person.account.email)
    const { account } = person
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(account.lastName)

    await users.act(account, 'resetTwoFactor')
    const dialog = page.getByTestId('reset-two-factor-dialog')
    await expect(dialog).toContainText(
      t('pages.admin.users.resetTwoFactor.message', { fullName: fullName(account) }),
    )
    await dialog.getByTestId('admin-password-confirm').click()
    await expect(dialog.getByTestId('admin-password-error')).toHaveText(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )

    await dialog.getByTestId('admin-password-input').fill(PASSWORD)
    const sentAfter = new Date()
    await dialog.getByTestId('admin-password-confirm').click()

    await expect(notification(page, t('pages.admin.users.toasts.twoFactorReset'))).toBeVisible()
    await expect(dialog).toBeHidden()
    expect((await currentUser(person.api)).twoFactorMethod).toBe('Email')
    await mailbox.waitFor({ to: account.email, subject: SUBJECTS.securityAlert, sentAfter })
  })

  test('a user the superadministrator deletes leaves the list and can no longer sign in', async ({
    page,
    pageAs,
    initialAdmin,
    accounts,
  }) => {
    const account = await accounts.create({ label: 'deleted' })
    const adminPage = await pageAs(initialAdmin)
    const users = new UsersAdminPage(adminPage)
    await users.open()
    await users.find(account.lastName)

    await users.act(account, 'delete')
    await expect(messageBox(adminPage)).toContainText(account.lastName)
    await confirm(adminPage)

    await expect(notification(adminPage, t('pages.admin.users.toasts.deleted'))).toBeVisible()
    await expect(users.row(account)).toBeHidden()
    await expect(users.table.root.getByTestId('data-table-empty')).toHaveText(t('table.noMatches'))
    expect(await findUser(initialAdmin.api, account.email)).toBeUndefined()
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toContainText(t('pages.login.error'))
  })
})
