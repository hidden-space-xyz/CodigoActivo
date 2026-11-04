import { firstDependent } from '../../support/api/accounts'
import { environment } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { fullName } from '../../support/data/people'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'
import { SiteHeader } from '../../support/pages/site-header'

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

    await expect(users.table.count).toHaveText('1 resultado')
    await expect(users.row(account)).toContainText(account.email)
    await expect(users.row(account)).toContainText('Activo')
    await expect(users.row(account)).toContainText('Participante')

    await users.table.root.getByRole('button', { name: 'Limpiar filtros' }).click()
    await users.table.searchColumn('Correo', account.email)

    await expect(users.table.chips.getByText(`Correo: ${account.email}`)).toBeVisible()
    await expect(users.row(account)).toBeVisible()
    await expect(users.table.count).toHaveText('1 resultado')
  })

  test('pressing Enter in a column search applies it and closes its panel', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    test.fixme(true, 'The panel starts closing on Enter and reopens about 25 ms later')
    const account = await accounts.create({ label: 'enter' })
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()

    const panel = await users.table.enterColumnSearch('Correo', account.email)

    await expect(users.table.chips.getByText(`Correo: ${account.email}`)).toBeVisible()
    await expect(panel).toBeHidden()
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

    await users.table.filterBy('Tipo', 'Participante')
    await expect(
      users.table.root.getByText('Ningún resultado coincide con los filtros.'),
    ).toBeVisible()
    await users.table.filterBy('Tipo', 'Socio')
    await users.table.filterBy('Estado', 'Activo')
    await users.table.filterBy('Admin', 'No')
    await users.table.filterBy('Novedades', 'Sí')
    await users.table.searchColumn('DNI/NIE', person.nationalId)

    await expect(users.row(person)).toBeVisible()
    await expect(users.table.count).toHaveText('1 resultado')
    for (const chip of [
      'Tipo: Socio',
      'Estado: Activo',
      'Admin: No',
      'Novedades: Sí',
      `DNI/NIE: ${person.nationalId}`,
    ]) {
      await expect(users.table.chips.getByText(chip, { exact: true })).toBeVisible()
    }

    await users.table.root.getByRole('button', { name: 'Limpiar filtros' }).click()

    await expect(users.table.chips).toBeHidden()
    await expect(users.table.root.getByRole('textbox', { name: 'Buscar por nombre' })).toHaveValue(
      '',
    )
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

    await expect(
      users.scopes.getByRole('radio', { name: 'Independientes', exact: true }),
    ).toBeChecked()
    await expect(users.table.count).toHaveText('1 resultado')
    await expect(users.table.root.getByRole('button', { name: 'Filtrar por Admin' })).toBeVisible()

    await users.showScope('Todos')
    await expect(users.table.count).toHaveText('3 resultados')

    await users.showScope('Dependientes')
    await expect(users.table.count).toHaveText('2 resultados')
    await expect(
      users.table.root.getByRole('button', { name: 'Filtrar por Nacimiento' }),
    ).toBeVisible()
    await expect(users.table.root.getByRole('button', { name: 'Filtrar por Admin' })).toBeHidden()
    await expect(users.table.root.getByRole('button', { name: 'Buscar por Correo' })).toBeHidden()
    for (const dependent of parent.dependents) {
      const row = users.row(dependent)
      await expect(row.getByText('Tutor:', { exact: true })).toBeVisible()
      await expect(row.getByRole('button', { name: fullName(parent) })).toBeVisible()
    }

    await users.showScope('Independientes')
    await users.row(parent).getByRole('button', { name: 'Dependientes: 2' }).click()

    const dependentsChip = `Dependientes de ${fullName(parent)}`
    await expect(users.table.chips.getByText(dependentsChip, { exact: true })).toBeVisible()
    await expect(
      users.scopes.getByRole('radio', { name: 'Dependientes', exact: true }),
    ).toBeChecked()
    await expect(users.table.count).toHaveText('2 resultados')

    const dependent = firstDependent(parent)
    await users
      .row(dependent)
      .getByRole('button', { name: fullName(parent) })
      .click()

    const tutorChip = `Tutor de ${fullName(dependent)}`
    await expect(users.table.chips.getByText(tutorChip, { exact: true })).toBeVisible()
    await expect(users.table.chips.getByText(dependentsChip)).toBeHidden()
    await expect(
      users.scopes.getByRole('radio', { name: 'Independientes', exact: true }),
    ).toBeChecked()
    await expect(users.table.count).toHaveText('1 resultado')
    await expect(users.row(parent)).toBeVisible()

    await users.table.chips.getByRole('button', { name: `Quitar el filtro «${tutorChip}»` }).click()

    await expect(users.table.chips).toBeHidden()
  })

  test('the own row is tagged and keeps its role and deletion out of reach', async ({
    pageAs,
    admin,
  }) => {
    const self = admin.account
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.find(self.lastName)

    await expect(users.row(self).getByText('Tú', { exact: true })).toBeVisible()
    await expect(users.row(self).getByText('Admin', { exact: true })).toBeVisible()
    await users.openMenu(self)

    await expect(users.menuItem('Cambiar tipo')).toHaveAccessibleName(
      /Debe cambiarlo otro administrador/,
    )
    await expect(users.menuItem('Cambiar tipo')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('Retirar administrador')).toHaveAccessibleName(
      /Debe cambiarlo otro administrador/,
    )
    await expect(users.menuItem('Retirar administrador')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('Eliminar')).toHaveAccessibleName(
      /Elimina tu cuenta desde «Mi cuenta»/,
    )
    await expect(users.menuItem('Eliminar')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('Escribir un correo')).not.toHaveAttribute('aria-disabled', 'true')
  })

  test('the initial administrator can be neither demoted nor deleted', async ({
    pageAs,
    admin,
    initialAdmin,
  }) => {
    const users = new UsersAdminPage(await pageAs(admin))
    await users.open()
    await users.table.searchColumn('Correo', environment.initialAdminEmail)

    const owner = initialAdmin.account
    await expect(users.row(owner).getByText('Admin', { exact: true })).toBeVisible()
    await expect(users.row(owner).getByText('Tú', { exact: true })).toBeHidden()
    await users.openMenu(owner)

    await expect(users.menuItem('Retirar administrador')).toHaveAccessibleName(
      /Cuenta de administración inicial/,
    )
    await expect(users.menuItem('Retirar administrador')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('Eliminar')).toHaveAccessibleName(
      /Cuenta de administración inicial/,
    )
    await expect(users.menuItem('Eliminar')).toHaveAttribute('aria-disabled', 'true')
    await expect(users.menuItem('Cambiar tipo')).not.toHaveAttribute('aria-disabled', 'true')
  })

  test('someone who is not an administrator is sent home from the user list', async ({
    pageAs,
    member,
  }) => {
    const page = await pageAs(member)

    await page.goto('/admin/users')

    await expect(page).toHaveURL('/')
    await expect(new SiteHeader(page).admin).toBeHidden()
  })
})
