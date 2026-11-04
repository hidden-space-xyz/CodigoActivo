import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { CertificatesTab } from '../../support/pages/account/certificates-tab'
import { ConsentsSection } from '../../support/pages/account/consents-section'
import { MinorsSection } from '../../support/pages/account/minors-section'
import { LoginPage } from '../../support/pages/login-page'

test.describe('account page', () => {
  test('a visitor is sent to log in before seeing the account', async ({ page }) => {
    await page.goto('/account')

    await expect(page).toHaveURL(/\/login\?redirect=\/account$/)
    await expect(new LoginPage(page).heading).toBeVisible()
  })

  test('the selected tab is kept in the address', async ({ pageAs, participant }) => {
    const page = await pageAs(participant)
    const account = new AccountPage(page)
    await account.goto()
    await expect(page).toHaveURL('/account')

    await account.tab('Historial').click()
    await expect(page).toHaveURL('/account?tab=history')
    await expect(page.getByText('Todas las inscripciones de tu unidad familiar')).toBeVisible()

    await account.tab('Diplomas').click()
    await expect(page).toHaveURL('/account?tab=certificates')
    await expect(page.getByText('Al terminar un evento se emite un diploma')).toBeVisible()

    await account.tab('Perfil').click()
    await expect(page).toHaveURL('/account')
    await expect(account.editData).toBeVisible()

    await account.goto('Historial')
    await expect(account.tab('Perfil')).toHaveAttribute('aria-selected', 'false')
  })

  test('a new account has no minors, consents, history or diplomas yet', async ({
    accounts,
    pageAs,
  }) => {
    const actor = await accounts.actor({ label: 'empty' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()

    await expect(new MinorsSection(account).empty).toBeVisible()
    await expect(new ConsentsSection(account).empty).toBeVisible()

    await account.tab('Historial').click()
    await expect(page.getByText('Todavía no tienes inscripciones en ningún evento.')).toBeVisible()

    await account.tab('Diplomas').click()
    await expect(new CertificatesTab(page).empty).toBeVisible()
  })
})
