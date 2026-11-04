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

  test('granting the admin role asks for the password and opens the administration', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'granted' })
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)

    await users.act(person.account, 'Conceder administrador')
    const dialog = page.getByRole('dialog', { name: 'Conceder rol de administrador' })
    const password = dialog.getByLabel('Tu contraseña')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByRole('button', { name: 'Conceder' }).click()

    await expect(dialog.getByText('La contraseña actual no es correcta.')).toBeVisible()
    await expect(password).toHaveAttribute('aria-invalid', 'true')

    await password.fill(PASSWORD)
    await dialog.getByRole('button', { name: 'Conceder' }).click()

    await expect(notification(page, 'Administrador concedido.')).toBeVisible()
    await expect(dialog).toBeHidden()
    await expect(users.row(person.account).getByText('Admin', { exact: true })).toBeVisible()
    const personPage = await pageAs(person)
    await personPage.goto('/')
    await expect(new SiteHeader(personPage).admin).toBeVisible()
  })

  test('revoking the admin role closes the administration to that person', async ({
    pageAs,
    admin,
    accounts,
  }) => {
    const person = await accounts.actor({ label: 'revoked', admin: true })
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.account.lastName)
    await expect(users.row(person.account).getByText('Admin', { exact: true })).toBeVisible()

    await users.act(person.account, 'Retirar administrador')
    await confirm(page, 'Retirar rol de administrador', 'Retirar')

    await expect(notification(page, 'Administrador retirado.')).toBeVisible()
    await expect(users.row(person.account).getByText('Admin', { exact: true })).toBeHidden()
    const personPage = await pageAs(person)
    await personPage.goto('/admin/users')
    await expect(personPage).toHaveURL('/')
    await expect(new SiteHeader(personPage).admin).toBeHidden()
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

  test('a deleted user leaves the list and can no longer sign in', async ({
    page,
    pageAs,
    admin,
    accounts,
  }) => {
    const account = await accounts.create({ label: 'deleted' })
    const adminPage = await pageAs(admin)
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
    expect(await findUser(admin.api, account.email)).toBeUndefined()
    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(account)
    await expect(login.error).toContainText('No hemos podido iniciar sesión')
  })
})
