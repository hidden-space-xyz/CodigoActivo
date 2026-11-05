import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { ContentAdminPage } from '../../support/pages/admin-content/content-admin-page'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

test.describe('news administration', () => {
  test('an administrator adds a news item with a description and an image', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'admin-news')
    await news.open()
    const title = uniqueTitle('Novedad')
    const subtitle = 'Resumen de la jornada de otoño'

    const form = await news.startCreate('news-form-dialog')
    await form.fill({ 'news-form-title': title, 'news-form-subtitle': subtitle })
    await form.write('news-form-description', 'Crónica completa de la jornada de talleres.')
    await form.pickImage()
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.news.toasts.created'))).toBeVisible()
    await news.search(title)
    await expect(news.table.row(title)).toContainText(subtitle)
  })

  test('saving an empty news item marks the missing fields and asks for an image', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'admin-news')
    await news.open()

    const form = await news.startCreate('news-form-dialog')
    await form.save()

    await expect(form.field('news-form-title')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.field('news-form-subtitle')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.field('thumbnail-field')).toContainText(t('common.imageRequired'))
    await expect(form.root).toBeVisible()
  })

  test('editing a news item saves the changes that its public page then shows', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'admin-news')
    await news.open()
    await news.search(item.title)
    const title = uniqueTitle('Novedad revisada')
    const text = `Texto revisado ${uniqueLetters()} con el horario definitivo.`

    await news.table.rowAction(item.title, 'edit')
    const form = news.form('news-form-dialog')
    await expect(form.field('news-form-title')).toHaveValue(item.title)
    await form.fill({ 'news-form-title': title })
    await form.write('news-form-description', text)
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.news.toasts.saved'))).toBeVisible()
    await news.search(title)
    await expect(news.table.row(title)).toBeVisible()
    await page.goto(`/news/${item.id}`)
    const detail = page.getByTestId('page-news-detail')
    await expect(detail).toContainText(title)
    await expect(detail).toContainText(text)
  })

  test('deleting a news item asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const page = await pageAs(admin)
    const news = new ContentAdminPage(page, 'admin-news')
    await news.open()
    await news.search(item.title)

    await news.table.rowAction(item.title, 'delete')
    await expect(messageBox(page)).toContainText(
      t('pages.admin.news.delete.message', { title: item.title }),
    )
    await confirm(page)

    await expect(notification(page, t('pages.admin.news.toasts.deleted'))).toBeVisible()
    await expect(news.table.row(item.title)).toHaveCount(0)
    await page.goto(`/news/${item.id}`)
    await expect(page.getByTestId('page-news-detail')).toContainText(t('pages.newsDetail.notFound'))
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
    const news = new ContentAdminPage(page, 'admin-news')
    await news.open()

    await news.search(word)
    await expect(news.table.row(bySubtitle.title)).toBeVisible()
    await expect(news.table.row(other.title)).toHaveCount(0)

    await news.search(other.title)
    await expect(news.table.row(other.title)).toBeVisible()
    await expect(news.table.row(bySubtitle.title)).toHaveCount(0)
    await expect(news.table.count).toHaveText(t('table.results', { n: 1 }))
  })
})
