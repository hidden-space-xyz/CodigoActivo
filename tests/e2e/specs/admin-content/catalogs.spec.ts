import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
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

    const form = await catalogs.categories.startCreate('Nueva categoría')
    await form.save()
    await expect(form.field('Nombre')).toHaveAttribute('aria-invalid', 'true')
    await form.field('Nombre').fill(name)
    await form.saveAndClose()

    await expect(notification(page, 'Categoría creada.')).toBeVisible()
    await catalogs.categories.search(name)
    await expect(catalogs.categories.table.row(name)).toContainText(/#6366F1/i)
  })

  test('a second category with the same name is refused', async ({ pageAs, admin, seed }) => {
    const existing = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()

    const form = await catalogs.categories.startCreate('Nueva categoría')
    await form.field('Nombre').fill(existing.name)
    await form.save()

    await expect(notification(page, 'Ya existe una categoría con ese nombre.')).toBeVisible()
    await expect(form.root).toBeVisible()
  })

  test('editing an event category renames it', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)
    const name = uniqueTitle('Categoría renombrada')

    await catalogs.categories.table.rowAction(category.name, 'Editar')
    const form = catalogs.form('Editar categoría')
    await expect(form.field('Nombre')).toHaveValue(category.name)
    await form.field('Nombre').fill(name)
    await form.saveAndClose()

    await expect(notification(page, 'Categoría actualizada.')).toBeVisible()
    await catalogs.categories.search(name)
    await expect(catalogs.categories.table.row(name)).toBeVisible()
  })

  test('deleting an unused event category removes it', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)

    await catalogs.categories.table.rowAction(category.name, 'Eliminar')
    await confirm(page, 'Eliminar categoría', 'Eliminar')

    await expect(notification(page, 'Categoría eliminada.')).toBeVisible()
    await expect(catalogs.categories.table.row(category.name)).toHaveCount(0)
  })

  test('the only category of an event cannot be deleted', async ({ pageAs, admin, seed }) => {
    const category = await seed.category()
    await seed.event({ categories: [category] })
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.categories.search(category.name)

    await catalogs.categories.table.rowAction(category.name, 'Eliminar')
    await confirm(page, 'Eliminar categoría', 'Eliminar')

    await expect(
      notification(page, 'No se puede eliminar: hay eventos cuya única categoría es esta.'),
    ).toBeVisible()
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

    const form = await catalogs.terms.startCreate('Nuevos términos y condiciones')
    await form.save()
    await expect(form.field('Nombre')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.root.getByText('El contenido es obligatorio.')).toBeVisible()
    await form.field('Nombre').fill(name)
    await form.write('Contenido', 'Cada participante respeta las normas del taller.')
    await setChecked(form.root.getByRole('radio', { name: 'Condiciones' }), true)
    await form.setSwitch(true)
    await form.saveAndClose()

    await expect(notification(page, 'Términos y condiciones creados.')).toBeVisible()
    await catalogs.terms.search(name)
    const row = catalogs.terms.table.row(name)
    await expect(row.getByText('Condiciones', { exact: true })).toBeVisible()
    await expect(row.getByText('Requiere firma', { exact: true })).toBeVisible()
    await expect(row.getByText('Versión 1', { exact: true })).toBeVisible()
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

    await catalogs.terms.table.rowAction(terms.name, 'Editar')
    const form = catalogs.form('Editar términos y condiciones')
    await expect(form.root.getByText(/^Es la versión 1\./)).toBeVisible()
    await expect(form.field('Nombre')).toHaveValue(terms.name)
    await setChecked(form.root.getByRole('radio', { name: 'Consentimiento' }), true)
    await form.saveAndClose()

    await expect(notification(page, 'Términos y condiciones actualizados.')).toBeVisible()
    const row = catalogs.terms.table.row(terms.name)
    await expect(row.getByText('Consentimiento', { exact: true })).toBeVisible()
    await expect(row.getByText('Versión 2', { exact: true })).toBeVisible()
  })

  test('terms and conditions an event uses cannot be deleted', async ({ pageAs, admin, seed }) => {
    const terms = await seed.termsDocument()
    await seed.event({ terms: [{ document: terms, required: true }] })
    const page = await pageAs(admin)
    const catalogs = new CatalogsPage(page)
    await catalogs.open()
    await catalogs.terms.search(terms.name)

    await catalogs.terms.table.rowAction(terms.name, 'Eliminar')
    await confirm(page, 'Eliminar términos y condiciones', 'Eliminar')

    await expect(
      notification(page, 'No se pueden eliminar estos términos y condiciones porque hay eventos'),
    ).toBeVisible()
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

    await catalogs.terms.table.rowAction(terms.name, 'Eliminar')
    await confirm(page, 'Eliminar términos y condiciones', 'Eliminar')

    await expect(notification(page, 'Términos y condiciones eliminados.')).toBeVisible()
    await expect(catalogs.terms.table.row(terms.name)).toHaveCount(0)
  })
})
