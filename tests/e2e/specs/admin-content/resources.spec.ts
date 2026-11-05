import { CATALOG } from '../../support/api/catalogs'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { ContentAdminPage } from '../../support/pages/admin-content/content-admin-page'
import { CardListPage } from '../../support/pages/public/card-list-page'
import { chooseOption, confirm, notification } from '../../support/ui/element-plus'

const INTERNAL = 'Interno'
const EXTERNAL = 'Externo'

test.describe('resource administration', () => {
  test('an internal resource needs a description and opens on its public page', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'admin-resources')
    await resources.open()
    const title = uniqueTitle('Recurso')
    const text = `Guía paso a paso ${uniqueLetters()} para el primer programa.`

    const form = await resources.startCreate('resource-form-dialog')
    await form.fill({
      'resource-form-title': title,
      'resource-form-subtitle': 'Material para empezar',
    })
    await chooseOption(form.field('resource-form-type'), CATALOG.resourceTypes.internal)
    await form.pickImage()
    await form.save()
    await expect(form.field('resource-form-description-error')).toHaveText(
      t('pages.admin.resources.form.problems.descriptionRequired'),
    )
    await form.write('resource-form-description', text)
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.resources.toasts.created'))).toBeVisible()
    await resources.search(title)
    await expect(resources.table.row(title)).toContainText(INTERNAL)
    const list = new CardListPage(page, '/resources')
    await list.goto()
    await list.searchFor(title)
    await list.card(title).click()
    const detail = page.getByTestId('page-resource-detail')
    await expect(detail).toContainText(title)
    await expect(detail).toContainText(text)
  })

  test('an external resource needs a valid link, which the lists open', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'admin-resources')
    await resources.open()
    const title = uniqueTitle('Enlace')
    const url = `https://example.org/recursos/${uniqueLetters()}`

    const form = await resources.startCreate('resource-form-dialog')
    await form.fill({
      'resource-form-title': title,
      'resource-form-subtitle': 'Documentación oficial',
    })
    await chooseOption(form.field('resource-form-type'), CATALOG.resourceTypes.external)
    await form.pickImage()
    await form.save()
    await expect(form.field('resource-form-url-error')).toHaveText(
      t('pages.admin.resources.form.problems.urlRequired'),
    )
    await expect(form.field('resource-form-url')).toHaveAttribute('aria-invalid', 'true')
    await form.field('resource-form-url').fill('example.org/recursos')
    await form.save()
    await expect(form.field('resource-form-url-error')).toHaveText(
      t('pages.admin.resources.form.problems.urlInvalid'),
    )
    await form.field('resource-form-url').fill(url)
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.resources.toasts.created'))).toBeVisible()
    await resources.search(title)
    const row = resources.table.row(title)
    await expect(row).toContainText(EXTERNAL)
    await expect(row.getByTestId('admin-resources-url')).toHaveAttribute('href', url)
    const list = new CardListPage(page, '/resources')
    await list.goto()
    await list.searchFor(title)
    await expect(list.card(title)).toHaveAttribute('href', url)
  })

  test('the type filter keeps the internal or the external resources', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const word = uniqueLetters()
    const internal = await seed.resource({ title: `Guía ${word}` })
    const external = await seed.resource({
      title: `Enlace ${word}`,
      url: `https://example.org/${word}`,
    })
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'admin-resources')
    await resources.open()
    await resources.search(word)
    await expect(resources.table.row(internal.title)).toBeVisible()
    await expect(resources.table.row(external.title)).toBeVisible()
    const type = t('pages.admin.resources.columns.type')

    await resources.table.filterBy('admin-resources-type-filter', CATALOG.resourceTypes.internal)
    await expect(resources.table.chips).toContainText(
      t('table.filterChip', { label: type, value: INTERNAL }),
    )
    await expect(resources.table.row(internal.title)).toBeVisible()
    await expect(resources.table.row(external.title)).toHaveCount(0)

    await resources.table.filterBy('admin-resources-type-filter', CATALOG.resourceTypes.external)
    await expect(resources.table.chips).toContainText(
      t('table.filterChip', { label: type, value: EXTERNAL }),
    )
    await expect(resources.table.row(external.title)).toBeVisible()
    await expect(resources.table.row(internal.title)).toHaveCount(0)
  })

  test('editing a resource saves its new title and subtitle', async ({ pageAs, admin, seed }) => {
    const resource = await seed.resource()
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'admin-resources')
    await resources.open()
    await resources.search(resource.title)
    const title = uniqueTitle('Recurso revisado')
    const subtitle = 'Segunda edición de la guía'

    await resources.table.rowAction(resource.title, 'edit')
    const form = resources.form('resource-form-dialog')
    await expect(form.field('resource-form-title')).toHaveValue(resource.title)
    await form.fill({ 'resource-form-title': title, 'resource-form-subtitle': subtitle })
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.resources.toasts.updated'))).toBeVisible()
    await resources.search(title)
    await expect(resources.table.row(title)).toContainText(subtitle)
  })

  test('deleting a resource asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const resource = await seed.resource({ url: `https://example.org/${uniqueLetters()}` })
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'admin-resources')
    await resources.open()
    await resources.search(resource.title)

    await resources.table.rowAction(resource.title, 'delete')
    await confirm(page)

    await expect(notification(page, t('pages.admin.resources.toasts.deleted'))).toBeVisible()
    await expect(resources.table.row(resource.title)).toHaveCount(0)
  })
})
