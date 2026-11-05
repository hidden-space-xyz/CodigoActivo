import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { CatalogsPage } from '../../support/pages/admin-content/catalogs-page'
import { confirm, notification, setChecked } from '../../support/ui/element-plus'

test.describe('catalog administration', () => {
  test('an administrator adds an event category with the default colour', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    const name = uniqueTitle('Categoría')

    const form = await catalogs.categories.startCreate('event-category-form-dialog')
    await form.save()
    await expect(form.field('event-category-fields-name')).toHaveAttribute('aria-invalid', 'true')
    await form.field('event-category-fields-name').fill(name)
    await form.saveAndClose()

    await expect(
      notification(page, t('pages.admin.catalogs.eventCategories.toasts.created')),
    ).toBeVisible()
    await catalogs.categories.search(name)
    await expect(catalogs.categories.table.row(name)).toContainText(/#6366F1/i)
  })

  test('a second category with the same name is refused', async ({ pageAs, admin, seed }) => {
    const existing = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()

    const form = await catalogs.categories.startCreate('event-category-form-dialog')
    await form.field('event-category-fields-name').fill(existing.name)
    await form.save()

    await expect(notification(page, t('errors.EventCategoryTypeNameAlreadyExists'))).toBeVisible()
    await expect(form.root).toBeVisible()
  })

  test('editing an event category renames it', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)
    const name = uniqueTitle('Categoría renombrada')

    await catalogs.categories.table.rowAction(category.name, 'edit')
    const form = catalogs.form('event-category-form-dialog')
    await expect(form.field('event-category-fields-name')).toHaveValue(category.name)
    await form.field('event-category-fields-name').fill(name)
    await form.saveAndClose()

    await expect(
      notification(page, t('pages.admin.catalogs.eventCategories.toasts.updated')),
    ).toBeVisible()
    await catalogs.categories.search(name)
    await expect(catalogs.categories.table.row(name)).toBeVisible()
  })

  test('deleting an unused event category removes it', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)

    await catalogs.categories.table.rowAction(category.name, 'delete')
    await confirm(page)

    await expect(
      notification(page, t('pages.admin.catalogs.eventCategories.toasts.deleted')),
    ).toBeVisible()
    await expect(catalogs.categories.table.row(category.name)).toHaveCount(0)
  })

  test('the only category of an event cannot be deleted', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    await seed.event({ categories: [category] })
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)

    await catalogs.categories.table.rowAction(category.name, 'delete')
    await confirm(page)

    await expect(notification(page, t('errors.EventCategoryTypeOnlyCategoryOfEvent'))).toBeVisible()
    await page.reload()
    await catalogs.categories.search(category.name)
    await expect(catalogs.categories.table.row(category.name)).toBeVisible()
  })

  test('an administrator adds terms and conditions that ask for a signature', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    const name = uniqueTitle('Condiciones')

    const form = await catalogs.terms.startCreate('terms-document-form-dialog')
    await form.save()
    await expect(form.field('terms-document-form-name')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.field('terms-document-form-content-error')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.form.problems.contentRequired'),
    )
    await form.field('terms-document-form-name').fill(name)
    await form.write(
      'terms-document-form-content',
      'Cada participante respeta las normas del taller.',
    )
    await setChecked(form.field('terms-document-form-conditions'), true)
    await form.setSwitch('terms-document-form-requires-signature', true)
    await form.saveAndClose()

    await expect(
      notification(page, t('pages.admin.catalogs.termsDocuments.toasts.created')),
    ).toBeVisible()
    await catalogs.terms.search(name)
    const row = catalogs.terms.table.row(name)
    await expect(row.getByTestId('terms-documents-kind')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.kinds.Conditions'),
    )
    await expect(row.getByTestId('terms-documents-requires-signature')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.requiresSignature'),
    )
    await expect(row.getByTestId('terms-documents-version')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.version', { version: 1 }),
    )
  })

  test('editing terms and conditions starts their next version', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const terms = await seed.termsDocument()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.terms.search(terms.name)

    await catalogs.terms.table.rowAction(terms.name, 'edit')
    const form = catalogs.form('terms-document-form-dialog')
    await expect(form.field('terms-document-form-version-hint')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.form.versionHint', { version: 1 }),
    )
    await expect(form.field('terms-document-form-name')).toHaveValue(terms.name)
    await setChecked(form.field('terms-document-form-consent'), true)
    await form.saveAndClose()

    await expect(
      notification(page, t('pages.admin.catalogs.termsDocuments.toasts.updated')),
    ).toBeVisible()
    const row = catalogs.terms.table.row(terms.name)
    await expect(row.getByTestId('terms-documents-kind')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.kinds.Consent'),
    )
    await expect(row.getByTestId('terms-documents-version')).toHaveText(
      t('pages.admin.catalogs.termsDocuments.version', { version: 2 }),
    )
  })

  test('terms and conditions an event uses cannot be deleted', async ({ pageAs, admin, seed }) => {
    const terms = await seed.termsDocument()
    await seed.event({ terms: [{ document: terms, required: true }] })
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.terms.search(terms.name)

    await catalogs.terms.table.rowAction(terms.name, 'delete')
    await confirm(page)

    await expect(notification(page, t('errors.TermsDocumentInUse'))).toBeVisible()
    await page.reload()
    await catalogs.terms.search(terms.name)
    await expect(catalogs.terms.table.row(terms.name)).toBeVisible()
  })

  test('deleting unused terms and conditions removes them', async ({ pageAs, admin, seed }) => {
    const terms = await seed.termsDocument()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.terms.search(terms.name)

    await catalogs.terms.table.rowAction(terms.name, 'delete')
    await confirm(page)

    await expect(
      notification(page, t('pages.admin.catalogs.termsDocuments.toasts.deleted')),
    ).toBeVisible()
    await expect(catalogs.terms.table.row(terms.name)).toHaveCount(0)
  })
})
