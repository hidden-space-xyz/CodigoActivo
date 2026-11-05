import { findUser } from '../../support/api/accounts'
import { signupRoleNames } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'
import { LoginPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { chooseOption, confirm, notification } from '../../support/ui/element-plus'

test.describe('admin user actions', () => {
  test('a participant made a member may then lead activities', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'promoted' })
    expect(await signupRoleNames(person.api, person.account.id)).not.toContain('Líder')
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)

    await users.act(person.account, 'Cambiar tipo')
    const dialog = page.getByRole('dialog', { name: 'Cambiar tipo de usuario' })
    await chooseOption(dialog.getByLabel('Tipo de usuario'), 'Socio')
    await dialog.getByRole('button', { name: 'Aplicar' }).click()

    await expect(notification(page, 'Tipo de usuario actualizado.')).toBeVisible()
    await expect(dialog).toBeHidden()
    await expect(users.row(person.account)).toContainText('Socio')
    expect(await signupRoleNames(person.api, person.account.id)).toContain('Líder')
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

    await users.act(person.account, 'Cambiar cargo')
    const dialog = page.getByRole('dialog', { name: 'Cambiar cargo' })
    await chooseOption(dialog.getByLabel('Cargo'), 'Administrador')
    const password = dialog.getByLabel('Tu contraseña')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByRole('button', { name: 'Cambiar cargo' }).click()

    await expect(dialog.getByText('La contraseña actual no es correcta.')).toBeVisible()
    await expect(password).toHaveAttribute('aria-invalid', 'true')

    await password.fill(PASSWORD)
    await dialog.getByRole('button', { name: 'Cambiar cargo' }).click()

    await expect(notification(page, 'Cargo actualizado.')).toBeVisible()
    await expect(dialog).toBeHidden()
    await expect(
      users.row(person.account).getByText('Administrador', { exact: true }),
    ).toBeVisible()
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

    await users.act(account, 'Cambiar cargo')
    const dialog = page.getByRole('dialog', { name: 'Cambiar cargo' })
    await chooseOption(dialog.getByLabel('Cargo'), 'Editor')
    await dialog.getByLabel('Tu contraseña').fill(PASSWORD)
    await dialog.getByRole('button', { name: 'Cambiar cargo' }).click()

    await expect(notification(page, /Solo un socio puede tener un cargo/)).toBeVisible()
    await expect(users.row(account).getByText('Editor', { exact: true })).toBeHidden()
  })

  test('leaving an account without a cargo closes the administration to that person', async ({
    pageAs,
    initialAdmin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'revoked', type: 'member', editor: true })
    const page = await pageAs(initialAdmin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)
    await expect(users.row(person.account).getByText('Editor', { exact: true })).toBeVisible()

    await users.act(person.account, 'Cambiar cargo')
    const dialog = page.getByRole('dialog', { name: 'Cambiar cargo' })
    await chooseOption(dialog.getByLabel('Cargo'), 'Sin cargo')
    await expect(dialog.getByLabel('Tu contraseña')).toBeHidden()
    await dialog.getByRole('button', { name: 'Cambiar cargo' }).click()

    await expect(notification(page, 'Cargo actualizado.')).toBeVisible()
    await expect(users.row(person.account).getByText('Editor', { exact: true })).toBeHidden()
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

    await expect(users.menuItem('Cambiar tipo')).toBeVisible()
    await expect(users.menuItem('Cambiar cargo')).toHaveCount(0)
    await expect(users.menuItem('Designar responsable de protección de datos')).toHaveCount(0)
    await expect(users.menuItem('Eliminar')).toHaveCount(0)
  })

  test('resetting the second factor needs the administrator password', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const account = await accounts.create({ label: 'reset-2fa' })
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(account.lastName)

    await users.act(account, 'Restablecer la verificación en dos pasos')
    const dialog = page.getByRole('dialog', { name: 'Restablecer la verificación en dos pasos' })
    await expect(dialog).toContainText('ya recibe los códigos por correo')
    await dialog.getByRole('button', { name: 'Restablecer' }).click()
    await expect(dialog.getByText('Introduce tu contraseña.')).toBeVisible()

    await dialog.getByLabel('Tu contraseña').fill(PASSWORD)
    await dialog.getByRole('button', { name: 'Restablecer' }).click()

    await expect(
      notification(
        page,
        'Verificación en dos pasos restablecida: volverá a recibir el código por correo.',
      ),
    ).toBeVisible()
    await expect(dialog).toBeHidden()
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

    await users.act(account, 'Eliminar')
    await expect(adminPage.getByRole('dialog', { name: 'Eliminar usuario' })).toContainText(
      account.lastName,
    )
    await confirm(adminPage, 'Eliminar usuario', 'Eliminar')

    await expect(notification(adminPage, 'Usuario eliminado.')).toBeVisible()
    await expect(users.row(account)).toBeHidden()
    await expect(
      users.table.root.getByText('Ningún resultado coincide con los filtros.'),
    ).toBeVisible()
    expect(await findUser(initialAdmin.api, account.email)).toBeUndefined()
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toContainText('No hemos podido iniciar sesión')
  })
})
