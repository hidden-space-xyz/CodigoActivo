import { expect, test } from '../../support/fixtures/test'

test.describe('unsubscribe from the news', () => {
  test('a link without a token cannot unsubscribe anyone', async ({ page }) => {
    await page.goto('/unsubscribe')

    const main = page.getByRole('main')
    await expect(main.getByRole('heading', { level: 1 })).toHaveText(
      'Darte de baja de las novedades',
    )
    await expect(main.getByRole('heading', { level: 2 })).toHaveText(
      'No hemos podido darte de baja',
    )
    await expect(main.getByRole('alert')).toHaveText(
      'El enlace para darte de baja no es válido o está incompleto.',
    )
    await expect(main.getByText(/escribiéndonos a contacto@e2e\.codigoactivo\.test/)).toBeVisible()
    await main.getByRole('link', { name: 'Volver al inicio' }).click()
    await expect(page).toHaveURL('/')
  })

  test('a forged token is refused by the API', async ({ page }) => {
    await page.goto('/unsubscribe#token=forged-token-from-the-e2e-suite')

    const main = page.getByRole('main')
    await expect(main.getByRole('heading', { level: 2 })).toHaveText(
      'No hemos podido darte de baja',
    )
    await expect(main.getByRole('alert')).toHaveText(
      'El enlace para darte de baja no es válido. Comprueba que lo has abierto entero desde el correo.',
    )
    await expect(page).toHaveURL('/unsubscribe')
  })
})
