import { versionInForce } from '../../support/api/admin-content'
import { uniqueLetters } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { LegalPoliciesPage } from '../../support/pages/admin-content/legal-policies-page'
import { confirm, dialog, notification } from '../../support/ui/element-plus'

test.describe('legal policies', () => {
  test.describe.configure({ mode: 'serial' })

  test('editing a policy shows the unpublished changes until they are discarded', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const inForce = `Política de privacidad e2e ${uniqueLetters()}.`
    await seed.publishPolicy('Privacy', inForce)
    const page = await pageAs(admin)
    const policies = new LegalPoliciesPage(page)
    await policies.open()
    const privacy = policies.policy('Política de privacidad')
    await expect(privacy.editor).toHaveText(inForce)
    await expect(privacy.unpublishedChanges).toBeHidden()
    await expect(privacy.publish).toBeDisabled()

    await privacy.write(`Borrador sin publicar ${uniqueLetters()}.`)
    await expect(privacy.unpublishedChanges).toBeVisible()
    await expect(privacy.publish).toBeEnabled()

    await privacy.discard.click()
    await expect(privacy.editor).toHaveText(inForce)
    await expect(privacy.unpublishedChanges).toBeHidden()
    await expect(privacy.discard).toBeDisabled()
  })

  test('a policy without text cannot be published', async ({ pageAs, admin, seed }) => {
    await seed.publishPolicy('Privacy', `Política de privacidad e2e ${uniqueLetters()}.`)
    const page = await pageAs(admin)
    const policies = new LegalPoliciesPage(page)
    await policies.open()
    const privacy = policies.policy('Política de privacidad')

    await privacy.clear()
    await expect(privacy.unpublishedChanges).toBeVisible()
    await privacy.publish.click()

    await expect(privacy.root.getByText('Escribe el texto de la política.')).toBeVisible()
    await expect(dialog(page, 'Publicar la política')).toBeHidden()
  })

  test('publishing the privacy policy puts a new version in force on the public page', async ({
    page,
    pageAs,
    admin,
    seed,
    anonymousApi,
  }) => {
    await seed.publishPolicy('Privacy', `Política de privacidad e2e ${uniqueLetters()}.`)
    const current = await versionInForce(anonymousApi, 'Privacy')
    const next = current + 1
    const text = `Tratamos tus datos para gestionar las inscripciones ${uniqueLetters()}.`
    const adminPage = await pageAs(admin)
    const policies = new LegalPoliciesPage(adminPage)
    await policies.open()
    const privacy = policies.policy('Política de privacidad')
    await expect(privacy.status(current)).toBeVisible()

    await privacy.write(text)
    await privacy.publish.click()
    await expect(dialog(adminPage, 'Publicar la política')).toContainText(
      `Se publicará la versión ${next}`,
    )
    await confirm(adminPage, 'Publicar la política', 'Publicar')

    await expect(notification(adminPage, 'Política publicada.')).toBeVisible()
    await expect(privacy.status(next)).toBeVisible()
    await expect(privacy.unpublishedChanges).toBeHidden()
    await expect(privacy.version(next)).toContainText('En vigor')
    await expect(privacy.version(current)).not.toContainText('En vigor')

    await page.goto('/privacy')
    await expect(
      page.getByRole('heading', { level: 1, name: 'Política de privacidad' }),
    ).toBeVisible()
    await expect(page.getByText(text)).toBeVisible()
    await expect(
      page.getByText(new RegExp(`^Última actualización: .+ \\(versión ${next}\\)$`)),
    ).toBeVisible()
    await expect(page.getByText('G00000000', { exact: true })).toBeVisible()
  })

  test('an earlier version can be read and loaded back into the editor', async ({
    pageAs,
    admin,
    seed,
    anonymousApi,
  }) => {
    const earlier = `Versión anterior de la política e2e ${uniqueLetters()}.`
    await seed.publishPolicy('Privacy', earlier)
    const version = await versionInForce(anonymousApi, 'Privacy')
    await seed.publishPolicy('Privacy', `Versión vigente de la política e2e ${uniqueLetters()}.`)
    const page = await pageAs(admin)
    const policies = new LegalPoliciesPage(page)
    await policies.open()
    const privacy = policies.policy('Política de privacidad')

    await privacy.version(version).getByRole('button', { name: 'Ver', exact: true }).click()
    const viewer = page.getByRole('dialog', { name: `Versión ${version}`, exact: true })
    await expect(viewer).toContainText(earlier)
    await viewer.getByRole('button', { name: 'Usar en el editor', exact: true }).click()

    await expect(viewer).toBeHidden()
    await expect(privacy.editor).toHaveText(earlier)
    await expect(privacy.unpublishedChanges).toBeVisible()
  })

  test('publishing the cookie policy updates the public cookie page', async ({
    page,
    pageAs,
    admin,
    seed,
    anonymousApi,
  }) => {
    await seed.publishPolicy('Cookies', `Política de cookies e2e ${uniqueLetters()}.`)
    const next = (await versionInForce(anonymousApi, 'Cookies')) + 1
    const text = `Solo usamos las cookies técnicas de la sesión ${uniqueLetters()}.`
    const adminPage = await pageAs(admin)
    const policies = new LegalPoliciesPage(adminPage)
    await policies.open()
    await policies.tab('Política de cookies').click()
    const cookies = policies.policy('Política de cookies')

    await cookies.write(text)
    await cookies.publish.click()
    await confirm(adminPage, 'Publicar la política', 'Publicar')

    await expect(notification(adminPage, 'Política publicada.')).toBeVisible()
    await expect(cookies.status(next)).toBeVisible()
    await expect(cookies.version(next)).toContainText('En vigor')
    await page.goto('/cookies')
    await expect(page.getByRole('heading', { level: 1, name: 'Política de cookies' })).toBeVisible()
    await expect(page.getByText(text)).toBeVisible()
    await expect(
      page.getByText(new RegExp(`^Última actualización: .+ \\(versión ${next}\\)$`)),
    ).toBeVisible()
  })
})
