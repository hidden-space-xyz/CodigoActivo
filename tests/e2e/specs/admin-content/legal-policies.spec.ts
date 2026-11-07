import { versionInForce } from '../../support/api/admin-content'
import { LEGAL_ENTITY } from '../../support/config/environment'
import { dayText } from '../../support/data/formats'
import { uniqueLetters } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { LegalPoliciesPage } from '../../support/pages/admin-content/legal-policies-page'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

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
    const privacy = policies.policy('privacy')
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
    const privacy = policies.policy('privacy')

    await privacy.clear()
    await expect(privacy.unpublishedChanges).toBeVisible()
    await privacy.publish.click()

    await expect(privacy.root.getByTestId('legal-policy-editor-error')).toHaveText(
      t('pages.admin.legalPolicies.form.problems.contentRequired'),
    )
    await expect(messageBox(page)).toBeHidden()
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
    const privacy = policies.policy('privacy')
    await expect(privacy.status(current)).toBeVisible()

    await privacy.write(text)
    await privacy.publish.click()
    await expect(messageBox(adminPage)).toContainText(
      t('pages.admin.legalPolicies.publish.message', { version: next }),
    )
    await confirm(adminPage)

    await expect(
      notification(adminPage, t('pages.admin.legalPolicies.toasts.published')),
    ).toBeVisible()
    await expect(privacy.status(next)).toBeVisible()
    await expect(privacy.unpublishedChanges).toBeHidden()
    const inForce = t('pages.admin.legalPolicies.versions.inForce')
    await expect(privacy.version(next)).toContainText(inForce)
    await expect(privacy.version(current)).not.toContainText(inForce)

    await page.goto('/privacy')
    const policy = page.getByTestId('page-privacy-policy')
    await expect(policy).toContainText(t('pages.privacyPolicy.title'))
    await expect(policy).toContainText(text)
    await expect(policy).toContainText(
      t('entities.legalPolicy.updated', {
        date: dayText(new Date().toISOString()),
        version: next,
      }),
    )
    await expect(policy).toContainText(LEGAL_ENTITY.taxId)
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
    const privacy = policies.policy('privacy')

    await privacy.version(version).getByTestId('legal-policy-editor-version-view').click()
    const viewer = page.getByTestId('legal-policy-version-dialog')
    await expect(viewer).toContainText(earlier)
    await viewer.getByTestId('legal-policy-version-dialog-restore').click()

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
    await policies.tab('cookies').click()
    const cookies = policies.policy('cookies')

    await cookies.write(text)
    await cookies.publish.click()
    await confirm(adminPage)

    await expect(
      notification(adminPage, t('pages.admin.legalPolicies.toasts.published')),
    ).toBeVisible()
    await expect(cookies.status(next)).toBeVisible()
    await expect(cookies.version(next)).toContainText(
      t('pages.admin.legalPolicies.versions.inForce'),
    )
    await page.goto('/cookies')
    const policy = page.getByTestId('page-cookie-policy')
    await expect(policy).toContainText(t('pages.cookiePolicy.title'))
    await expect(policy).toContainText(text)
    await expect(policy).toContainText(
      t('entities.legalPolicy.updated', {
        date: dayText(new Date().toISOString()),
        version: next,
      }),
    )
  })
})
