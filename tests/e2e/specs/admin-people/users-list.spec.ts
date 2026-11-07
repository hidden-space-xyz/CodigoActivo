import { firstDependent } from '../../support/api/accounts'
import { CATALOG, USER_STATUS_LABELS, USER_TYPE_LABELS } from '../../support/api/catalogs'
import { environment } from '../../support/config/environment'
import { fullName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'

test.describe('admin user list', () => {
  test('an administrator finds a new account by its name and by its email', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const account = await accounts.create({ label: 'listed' })
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()

    await users.find(account.lastName)

    await expect(users.table.count).toHaveText(t('table.results', { n: 1 }))
    await expect(users.row(account)).toContainText(account.email)
    await expect(users.row(account)).toContainText(USER_STATUS_LABELS.active)
    await expect(users.row(account)).toContainText(USER_TYPE_LABELS.participant)

    await users.table.clear.click()
    await users.table.searchColumn('users-filter-email', account.email)

    await expect(
      users.table.chip(t('table.filterChip', { label: t('common.email'), value: account.email })),
    ).toBeVisible()
    await expect(users.row(account)).toBeVisible()
    await expect(users.table.count).toHaveText(t('table.results', { n: 1 }))
  })

  test('the status, type, admin and news filters narrow the list and show their chips', async ({
    pageAs,
    admin,
    member,
  }) => {
    const person = member.account
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.find(person.lastName)
    await expect(users.row(person)).toBeVisible()

    await users.table.filterBy('users-filter-type', CATALOG.userTypes.participant)
    await expect(users.table.root.getByTestId('data-table-empty')).toHaveText(t('table.noMatches'))
    await users.table.filterBy('users-filter-type', CATALOG.userTypes.member)
    await users.table.filterBy('users-filter-status', CATALOG.userStatuses.active)
    await users.table.filterBy('users-filter-staff-role', 'None')
    await users.table.filterBy('users-filter-news-consent', 'true')
    await users.table.searchColumn('users-filter-national-id', person.nationalId)

    await expect(users.row(person)).toBeVisible()
    await expect(users.table.count).toHaveText(t('table.results', { n: 1 }))
    for (const chip of [
      t('table.filterChip', {
        label: t('pages.admin.users.columns.type'),
        value: USER_TYPE_LABELS.member,
      }),
      t('table.filterChip', { label: t('common.status'), value: USER_STATUS_LABELS.active }),
      t('table.filterChip', {
        label: t('pages.admin.users.columns.staffRole'),
        value: t('entities.user.staffRole.None'),
      }),
      t('table.filterChip', {
        label: t('pages.admin.users.columns.newsConsent'),
        value: t('common.yes'),
      }),
      t('table.filterChip', {
        label: t('pages.admin.users.columns.nationalId'),
        value: person.nationalId,
      }),
    ]) {
      await expect(users.table.chip(chip)).toBeVisible()
    }

    await users.table.clear.click()

    await expect(users.table.chips).toBeHidden()
    await expect(users.table.root.getByTestId('data-table-search')).toHaveValue('')
  })

  test('the scopes split guardians from their dependents and link them to each other', async ({
    pageAs,
    admin,
    guardian,
  }) => {
    const parent = guardian.account
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.find(parent.lastName)

    await expect(users.scope('independent')).toBeChecked()
    await expect(users.table.count).toHaveText(t('table.results', { n: 1 }))
    await expect(users.table.root.getByTestId('users-filter-staff-role')).toBeVisible()

    await users.showScope('all')
    await expect(users.table.count).toHaveText(t('table.results', { n: 3 }))

    await users.showScope('dependent')
    await expect(users.table.count).toHaveText(t('table.results', { n: 2 }))
    await expect(users.table.root.getByTestId('users-filter-birth-date')).toBeVisible()
    await expect(users.table.root.getByTestId('users-filter-staff-role')).toBeHidden()
    await expect(users.table.root.getByTestId('users-filter-email')).toBeHidden()
    for (const dependent of parent.dependents) {
      const row = users.row(dependent)
      await expect(row.getByTestId('users-guardian-prefix')).toHaveText(
        t('pages.admin.users.guardianPrefix'),
      )
      await expect(row.getByTestId('users-guardian')).toHaveText(fullName(parent))
    }

    await users.showScope('independent')
    const dependents = users.row(parent).getByTestId('users-dependents')
    await expect(dependents).toHaveText(t('pages.admin.users.dependentsLabel', { count: 2 }))
    await dependents.click()

    const dependentsChip = t('pages.admin.users.relation.dependentsOf', {
      fullName: fullName(parent),
    })
    await expect(users.table.chip(dependentsChip)).toBeVisible()
    await expect(users.scope('dependent')).toBeChecked()
    await expect(users.table.count).toHaveText(t('table.results', { n: 2 }))

    const dependent = firstDependent(parent)
    await users.row(dependent).getByTestId('users-guardian').click()

    const tutorChip = t('pages.admin.users.relation.tutorOf', { fullName: fullName(dependent) })
    await expect(users.table.chip(tutorChip)).toBeVisible()
    await expect(users.table.chip(dependentsChip)).toBeHidden()
    await expect(users.scope('independent')).toBeChecked()
    await expect(users.table.count).toHaveText(t('table.results', { n: 1 }))
    await expect(users.row(parent)).toBeVisible()

    await users.table.chip(tutorChip).getByTestId('data-table-chip-remove').click()

    await expect(users.table.chips).toBeHidden()
  })

  test('the own row is tagged with its cargo and keeps its type out of reach', async ({
    pageAs,
    admin,
  }) => {
    const self = admin.account
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.find(self.lastName)

    await expect(users.row(self).getByTestId('users-you')).toBeVisible()
    await expect(users.row(self).getByTestId('users-staff-role')).toHaveText(
      t('entities.user.staffRole.Administrator'),
    )
    await users.openMenu(self)

    await expect(users.menuItem('changeType')).toHaveAccessibleName(
      new RegExp(t('pages.admin.users.blocked.selfRole')),
    )
    await expect(users.menuItem('changeType')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('staffRole')).toHaveCount(0)
    await expect(users.menuItem('delete')).toHaveCount(0)
    await expect(users.menuItem('sendEmail')).not.toHaveAttribute('aria-disabled', 'true')
  })

  test('the superadministrator keeps its own cargo and account out of reach', async ({
    pageAs,
    initialAdmin,
  }) => {
    const users = new UsersAdminPage(await pageAs(initialAdmin))
    await users.open()
    await users.table.searchColumn('users-filter-email', environment.initialAdminEmail)

    const owner = initialAdmin.account
    await expect(users.row(owner).getByTestId('users-staff-role')).toHaveText(
      t('pages.admin.users.staffRole.superAdmin'),
    )
    await expect(users.row(owner).getByTestId('users-you')).toBeVisible()
    await users.openMenu(owner)

    await expect(users.menuItem('staffRole')).toHaveCount(0)
    await expect(users.menuItem('delete')).toHaveAccessibleName(
      new RegExp(t('pages.admin.users.blocked.initialAdmin')),
    )
    await expect(users.menuItem('delete')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('privacyOfficer')).not.toHaveAttribute('aria-disabled', 'true')
  })
})
