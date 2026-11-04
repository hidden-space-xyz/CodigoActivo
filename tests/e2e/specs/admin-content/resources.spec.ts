import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { ContentAdminPage } from '../../support/pages/admin-content/content-admin-page'
import { chooseOption, confirm, notification } from '../../support/ui/element-plus'

test.describe('resource administration', () => {
  test('an internal resource needs a description and opens on its public page', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'Recursos')
    await resources.open()
    const title = uniqueTitle('Recurso')
    const text = `Guía paso a paso ${uniqueLetters()} para el primer programa.`

    const form = await resources.startCreate('Nuevo recurso')
    await form.fill({ Título: title, Subtítulo: 'Material para empezar' })
    await chooseOption(form.field('Tipo'), 'Interno')
    await form.pickImage()
    await form.save()
    await expect(
      form.root.getByText('La descripción es obligatoria en los recursos internos.'),
    ).toBeVisible()
    await form.write('Descripción', text)
    await form.saveAndClose()

    await expect(notification(page, 'Recurso creado.')).toBeVisible()
    await resources.search(title)
    await expect(resources.table.row(title)).toContainText('Interno')
    await page.goto('/resources')
    const search = page.getByRole('textbox', { name: 'Buscar por título o subtítulo' })
    await search.fill(title)
    await search.press('Enter')
    await page.getByRole('link', { name: new RegExp(title) }).click()
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
    await expect(page.getByText(text)).toBeVisible()
  })

  test('an external resource needs a valid link, which the lists open', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'Recursos')
    await resources.open()
    const title = uniqueTitle('Enlace')
    const url = `https://example.org/recursos/${uniqueLetters()}`

    const form = await resources.startCreate('Nuevo recurso')
    await form.fill({ Título: title, Subtítulo: 'Documentación oficial' })
    await chooseOption(form.field('Tipo'), 'Externo')
    await form.pickImage()
    await form.save()
    await expect(
      form.root.getByText('El enlace es obligatorio en los recursos externos.'),
    ).toBeVisible()
    await expect(form.field('Enlace')).toHaveAttribute('aria-invalid', 'true')
    await form.field('Enlace').fill('example.org/recursos')
    await form.save()
    await expect(
      form.root.getByText('El enlace debe ser una URL válida que empiece por http:// o https://.'),
    ).toBeVisible()
    await form.field('Enlace').fill(url)
    await form.saveAndClose()

    await expect(notification(page, 'Recurso creado.')).toBeVisible()
    await resources.search(title)
    const row = resources.table.row(title)
    await expect(row).toContainText('Externo')
    await expect(row.getByRole('link', { name: /example\.org\/recursos/ })).toHaveAttribute(
      'href',
      url,
    )
    await page.goto('/resources')
    const search = page.getByRole('textbox', { name: 'Buscar por título o subtítulo' })
    await search.fill(title)
    await search.press('Enter')
    await expect(page.getByRole('link', { name: new RegExp(title) })).toHaveAttribute('href', url)
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
    const resources = new ContentAdminPage(page, 'Recursos')
    await resources.open()
    await resources.search(word)
    await expect(resources.table.row(internal.title)).toBeVisible()
    await expect(resources.table.row(external.title)).toBeVisible()

    await resources.table.filterBy('Tipo', 'Interno')
    await expect(resources.table.chips).toContainText('Tipo: Interno')
    await expect(resources.table.row(internal.title)).toBeVisible()
    await expect(resources.table.row(external.title)).toHaveCount(0)

    await resources.table.filterBy('Tipo', 'Externo')
    await expect(resources.table.chips).toContainText('Tipo: Externo')
    await expect(resources.table.row(external.title)).toBeVisible()
    await expect(resources.table.row(internal.title)).toHaveCount(0)
  })

  test('editing a resource saves its new title and subtitle', async ({ pageAs, admin, seed }) => {
    const resource = await seed.resource()
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'Recursos')
    await resources.open()
    await resources.search(resource.title)
    const title = uniqueTitle('Recurso revisado')

    await resources.table.rowAction(resource.title, 'Editar')
    const form = resources.form('Editar recurso')
    await expect(form.field('Título')).toHaveValue(resource.title)
    await form.fill({ Título: title, Subtítulo: 'Segunda edición de la guía' })
    await form.saveAndClose()

    await expect(notification(page, 'Recurso actualizado.')).toBeVisible()
    await resources.search(title)
    await expect(resources.table.row(title)).toContainText('Segunda edición de la guía')
  })

  test('deleting a resource asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const resource = await seed.resource({ url: `https://example.org/${uniqueLetters()}` })
    const page = await pageAs(admin)
    const resources = new ContentAdminPage(page, 'Recursos')
    await resources.open()
    await resources.search(resource.title)

    await resources.table.rowAction(resource.title, 'Eliminar')
    await confirm(page, 'Eliminar recurso', 'Eliminar')

    await expect(notification(page, 'Recurso eliminado.')).toBeVisible()
    await expect(resources.table.row(resource.title)).toHaveCount(0)
  })
})
