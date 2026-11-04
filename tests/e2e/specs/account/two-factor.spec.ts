import { currentUser } from '../../support/api/account'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import {
  AuthenticatorSetupDialog,
  DisableAuthenticatorDialog,
  TwoFactorSection,
} from '../../support/pages/account/two-factor-section'
import { LoginPage, TwoFactorPage } from '../../support/pages/login-page'
import { SiteHeader } from '../../support/pages/site-header'
import { notification } from '../../support/ui/element-plus'

const INVALID_CODE = 'El código no es válido o ha caducado.'

test.describe('two-step verification', () => {
  test('a person uses the emailed code until they choose an application', async ({
    pageAs,
    member,
  }) => {
    const account = new AccountPage(await pageAs(member))
    await account.goto()
    const twoFactor = new TwoFactorSection(account)

    await expect(twoFactor.method).toHaveText('Código por correo electrónico')
    await expect(twoFactor.useAuthenticator).toBeVisible()
    await expect(twoFactor.useEmail).toBeHidden()
  })

  test('the setup refuses a wrong password and a wrong first code', async ({
    accounts,
    pageAs,
  }) => {
    const actor = await accounts.actor({ label: 'totp-refused' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const twoFactor = new TwoFactorSection(account)
    await twoFactor.useAuthenticator.click()
    const setup = new AuthenticatorSetupDialog(page)

    await setup.continue.click()
    await expect(setup.root.getByText('Introduce tu contraseña.')).toBeVisible()
    await setup.password.fill(`${PASSWORD}-wrong`)
    await setup.continue.click()
    await expect(setup.error).toHaveText('La contraseña actual no es correcta.')
    await expect(setup.sharedKey).toBeHidden()

    await setup.unlock(PASSWORD)
    await setup.activate.click()
    await expect(setup.root.getByText('Introduce el código.')).toBeVisible()
    await setup.code.fill('000000')
    await setup.activate.click()
    await expect(setup.error).toHaveText(INVALID_CODE)

    await setup.root.getByRole('button', { name: 'Cancelar' }).click()
    await expect(setup.root).toBeHidden()
    await expect(twoFactor.method).toHaveText('Código por correo electrónico')
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Email')
  })

  test('an authenticator application replaces the emailed code and can be dropped', async ({
    accounts,
    pageAs,
    page,
  }) => {
    test.slow()
    const actor = await accounts.actor({ label: 'totp' })
    const accountPage = await pageAs(actor)
    const account = new AccountPage(accountPage)
    await account.goto()
    const twoFactor = new TwoFactorSection(account)

    await twoFactor.useAuthenticator.click()
    const setup = new AuthenticatorSetupDialog(accountPage)
    const app = await setup.unlock(PASSWORD)
    await setup.code.fill(await app.nextCode())
    await setup.activate.click()

    await expect(notification(accountPage, 'Aplicación activada')).toBeVisible()
    await expect(setup.root).toBeHidden()
    await expect(twoFactor.method).toHaveText('Aplicación de autenticación')
    await expect(twoFactor.useEmail).toBeVisible()
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Authenticator')

    const login = new LoginPage(page)
    await login.goto()
    await login.submitPassword(actor.account)
    const verify = new TwoFactorPage(page)
    await expect(verify.heading).toBeVisible()
    await expect(
      page.getByText('Introduce el código que muestra tu aplicación de autenticación.'),
    ).toBeVisible()
    await verify.enter(app.lastCode())
    await expect(verify.error).toHaveText(INVALID_CODE)
    await verify.enter(await app.nextCode())
    await expect(page).not.toHaveURL(/\/login/)
    await expect(new SiteHeader(page).greeting(actor.account.firstName)).toBeVisible()

    await twoFactor.useEmail.click()
    const disable = new DisableAuthenticatorDialog(accountPage)
    await disable.submit.click()
    await expect(disable.root.getByText('Introduce tu contraseña.')).toBeVisible()
    await expect(disable.root.getByText('Introduce el código.')).toBeVisible()
    await disable.password.fill(PASSWORD)
    await disable.code.fill('000000')
    await disable.submit.click()
    await expect(disable.error).toHaveText(INVALID_CODE)
    await disable.code.fill(await app.nextCode())
    await disable.submit.click()

    await expect(notification(accountPage, 'Verificación por correo')).toBeVisible()
    await expect(disable.root).toBeHidden()
    await expect(twoFactor.method).toHaveText('Código por correo electrónico')
    expect((await currentUser(actor.api)).twoFactorMethod).toBe('Email')
  })
})
