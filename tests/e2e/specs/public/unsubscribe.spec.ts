import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'

const LEGAL_EMAIL = 'contacto@e2e.codigoactivo.test'

test.describe('unsubscribe from the news', () => {
  test('a link without a token cannot unsubscribe anyone', async ({ page }) => {
    await page.goto('/unsubscribe')

    const main = page.getByTestId('page-unsubscribe')
    await expect(main.getByTestId('page-head-title')).toHaveText(t('pages.unsubscribe.title'))
    await expect(main.getByTestId('status-panel-title')).toHaveText(
      t('pages.unsubscribe.errorTitle'),
    )
    await expect(main.getByTestId('status-panel-text')).toHaveText(
      t('pages.unsubscribe.incompleteLink'),
    )
    await expect(main.getByTestId('status-panel-hint')).toHaveText(
      t('pages.unsubscribe.hint', { email: LEGAL_EMAIL }),
    )
    await main.getByTestId('unsubscribe-home').click()
    await expect(page).toHaveURL('/')
  })

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
