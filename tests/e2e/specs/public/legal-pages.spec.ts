import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'

const LEGAL_ENTITY = [
  'Código Activo (e2e)',
  'G00000000',
  'León (e2e)',
  'Sin inscripción (e2e)',
  'contacto@e2e.codigoactivo.test',
  '600 00 00 00',
]

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
    await expect(controller.getByTestId('legal-entry-value')).toHaveText(LEGAL_ENTITY)
  })

  test('the cookie policy has its own page', async ({ page }) => {
    await page.goto('/cookies')

    const main = page.getByTestId('page-cookie-policy')
    await expect(main.getByTestId('page-head-title')).toHaveText(t('pages.cookiePolicy.title'))
    await expect(main.getByTestId('page-head-eyebrow')).toHaveText(t('pages.cookiePolicy.eyebrow'))
    await expect(page.getByTestId('privacy-controller')).toBeHidden()
  })
})
