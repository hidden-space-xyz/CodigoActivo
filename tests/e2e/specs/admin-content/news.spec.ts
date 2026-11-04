import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { ContentAdminPage } from '../../support/pages/admin-content/content-admin-page'
import { confirm, dialog, notification } from '../../support/ui/element-plus'

test.describe('news administration', () => {
  test('an administrator adds a news item with a description and an image', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'Novedades')
    await news.open()
    const title = uniqueTitle('Novedad')

    const form = await news.startCreate('Añadir novedad')
    await form.fill({ Título: title, Subtítulo: 'Resumen de la jornada de otoño' })
    await form.write('Descripción', 'Crónica completa de la jornada de talleres.')
    await form.pickImage()
    await form.saveAndClose()

    await expect(notification(page, 'Creado correctamente.')).toBeVisible()
    await news.search(title)
    await expect(news.table.row(title)).toContainText('Resumen de la jornada de otoño')
  })

  test('saving an empty news item marks the missing fields and asks for an image', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'Novedades')
    await news.open()

    const form = await news.startCreate('Añadir novedad')
    await form.save()

    await expect(form.field('Título')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.field('Subtítulo')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.root.getByText('La imagen es obligatoria.')).toBeVisible()
    await expect(form.root).toBeVisible()
  })

  test('editing a news item saves the changes that its public page then shows', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'Novedades')
    await news.open()
    await news.search(item.title)
    const title = uniqueTitle('Novedad revisada')
    const text = `Texto revisado ${uniqueLetters()} con el horario definitivo.`

    await news.table.rowAction(item.title, 'Editar')
    const form = news.form('Editar novedad')
    await expect(form.field('Título')).toHaveValue(item.title)
    await form.fill({ Título: title })
    await form.write('Descripción', text)
    await form.saveAndClose()

    await expect(notification(page, 'Cambios guardados.')).toBeVisible()
    await news.search(title)
    await expect(news.table.row(title)).toBeVisible()
    await page.goto(`/news/${item.id}`)
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
    await expect(page.getByText(text)).toBeVisible()
  })

  test('deleting a news item asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'Novedades')
    await news.open()
    await news.search(item.title)

    await news.table.rowAction(item.title, 'Eliminar', `Más acciones para ${item.title}`)
    await expect(dialog(page, 'Eliminar novedad')).toContainText(
      `¿Seguro que quieres eliminar "${item.title}"?`,
    )
    await confirm(page, 'Eliminar novedad', 'Eliminar')

    await expect(notification(page, 'Eliminado.')).toBeVisible()
    await expect(news.table.row(item.title)).toHaveCount(0)
    await page.goto(`/news/${item.id}`)
    await expect(page.getByText('No hemos encontrado esa novedad.')).toBeVisible()
  })

  test('the search finds news items by their title or their subtitle', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const word = uniqueLetters()
    const bySubtitle = await seed.newsItem({ subtitle: `Crónica ${word}` })
    const other = await seed.newsItem()
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'Novedades')
    await news.open()

    await news.search(word)
    await expect(news.table.row(bySubtitle.title)).toBeVisible()
    await expect(news.table.row(other.title)).toHaveCount(0)

    await news.search(other.title)
    await expect(news.table.row(other.title)).toBeVisible()
    await expect(news.table.row(bySubtitle.title)).toHaveCount(0)
    await expect(news.table.count).toHaveText('1 resultado')
  })
})
