import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { CertificatesTab } from '../../support/pages/account/certificates-tab'
import { ConsentsSection } from '../../support/pages/account/consents-section'
import { MinorsSection } from '../../support/pages/account/minors-section'

test.describe('account page', () => {
  test('the selected tab is kept in the address', async ({ pageAs, participant }) => {
    const page = await pageAs(participant)
    const account = new AccountPage(page)
    await account.goto()
    await expect(page).toHaveURL('/account')

    await account.tab('history').click()
    await expect(page).toHaveURL('/account?tab=history')
    await expect(page.getByTestId('history-section')).toBeVisible()

    await account.tab('certificates').click()
    await expect(page).toHaveURL('/account?tab=certificates')
    await expect(page.getByTestId('certificates-section')).toBeVisible()

    await account.tab('profile').click()
    await expect(page).toHaveURL('/account')
    await expect(account.editData).toBeVisible()

    await account.goto('history')
    await expect(account.tab('profile')).toHaveAttribute('aria-selected', 'false')
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

    await account.tab('history').click()
    await expect(page.getByTestId('history-empty')).toBeVisible()

    await account.tab('certificates').click()
    await expect(new CertificatesTab(page).empty).toBeVisible()
  })
})
