import { expect, test } from '../../support/fixtures/test'

test.describe('legal pages', () => {
  test('the privacy policy names the data controller the deployment configures', async ({
    page,
  }) => {
    await page.goto('/privacy')

    await expect(
      page.getByRole('heading', { level: 1, name: 'Política de privacidad' }),
    ).toBeVisible()
    const controller = page.getByRole('region', {
      name: '¿Quién es el responsable del tratamiento?',
    })
    await expect(controller.getByRole('term')).toHaveText([
      'Denominación',
      'NIF',
      'Domicilio',
      'Inscripción',
      'Correo electrónico',
      'Teléfono',
    ])
    await expect(controller.getByRole('definition')).toHaveText([
      'Código Activo (e2e)',
      'G00000000',
      'León (e2e)',
      'Sin inscripción (e2e)',
      'contacto@e2e.codigoactivo.test',
      '600 00 00 00',
    ])
  })

  test('the cookie policy has its own page', async ({ page }) => {
    await page.goto('/cookies')

    await expect(page.getByRole('heading', { level: 1, name: 'Política de cookies' })).toBeVisible()
    await expect(page.getByRole('main').getByText('// cookies')).toBeVisible()
    await expect(
      page.getByRole('region', { name: '¿Quién es el responsable del tratamiento?' }),
    ).toBeHidden()
  })
})
