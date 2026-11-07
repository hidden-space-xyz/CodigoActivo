import { LEGAL_ENTITY } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'

test.describe('legal pages', () => {
  test('the privacy policy names the data controller the deployment configures', async ({
    page,
  }) => {
    await page.goto('/privacy')

    const main = page.getByTestId('page-privacy-policy')
    await expect(main.getByTestId('page-head-title')).toHaveText(t('pages.privacyPolicy.title'))
    const controller = main.getByTestId('privacy-controller')
    await expect(controller).toHaveAccessibleName(t('pages.privacyPolicy.controller.title'))
    await expect(controller.getByTestId('legal-entry-label')).toHaveText([
      t('pages.privacyPolicy.controller.name'),
      t('pages.privacyPolicy.controller.taxId'),
      t('pages.privacyPolicy.controller.address'),
      t('pages.privacyPolicy.controller.registry'),
      t('pages.privacyPolicy.controller.email'),
      t('pages.privacyPolicy.controller.phone'),
    ])
    await expect(controller.getByTestId('legal-entry-value')).toHaveText([
      LEGAL_ENTITY.name,
      LEGAL_ENTITY.taxId,
      LEGAL_ENTITY.address,
      LEGAL_ENTITY.registry,
      LEGAL_ENTITY.email,
      LEGAL_ENTITY.phone,
    ])
  })
})
