import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'

test.describe('unsubscribe from the news', () => {
  test('a forged token is refused by the API', async ({ page }) => {
    await page.goto('/unsubscribe#token=forged-token-from-the-e2e-suite')

    const main = page.getByTestId('page-unsubscribe')
    await expect(main.getByTestId('status-panel-title')).toHaveText(
      t('pages.unsubscribe.errorTitle'),
    )
    await expect(main.getByTestId('status-panel-text')).toHaveText(
      t('errors.EmailUnsubscribeLinkInvalid'),
    )
    await expect(page).toHaveURL('/unsubscribe')
  })
})
