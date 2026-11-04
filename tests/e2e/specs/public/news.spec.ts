import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { CardListPage } from '../../support/pages/public/card-list-page'
import { cardDate, dayText } from '../../support/data/formats'

test.describe('news archive', () => {
  test('the archive opens on the newest year and lists its news as cards', async ({
    page,
    seed,
  }) => {
    const item = await seed.newsItem()
    const news = new CardListPage(page, '/news')
    await news.goto()

    await expect(news.year(item.createdAt.slice(0, 4))).toHaveAttribute('aria-pressed', 'true')
    await news.searchFor(item.title)
    const card = news.card(item.title)
    await expect(card.getByRole('time')).toHaveText(cardDate('Publicada', dayText(item.createdAt)))
    await expect(card).toContainText(item.subtitle)
    await expect(card.getByRole('img', { name: item.title })).toBeVisible()
    await card.click()
    await expect(page).toHaveURL(`/news/${item.id}`)
  })

  test('typing applies the search once 300 ms pass without keystrokes', async ({ page, seed }) => {
    const prefix = uniqueTitle('Novedad')
    const wanted = await seed.newsItem({ title: `${prefix} alfa` })
    const other = await seed.newsItem({ title: `${prefix} beta` })
    await page.clock.install()
    const news = new CardListPage(page, '/news')
    await news.goto()
    await expect(news.card(other.title)).toBeVisible()
    await page.clock.pauseAt(Date.now() + 1_000)

    await news.search.fill(wanted.title)
    await page.clock.runFor(299)

    expect(news.searches).toStrictEqual([])
    await page.clock.runFor(1)
    await expect.poll(() => news.searches).toStrictEqual([wanted.title])
    await page.clock.resume()
    await expect(news.card(wanted.title)).toBeVisible()
    await expect(news.card(other.title)).toBeHidden()
  })

  test('Enter applies the search without waiting', async ({ page, seed }) => {
    const prefix = uniqueTitle('Novedad')
    const wanted = await seed.newsItem({ title: `${prefix} alfa` })
    const other = await seed.newsItem({ title: `${prefix} beta` })
    await page.clock.install()
    const news = new CardListPage(page, '/news')
    await news.goto()
    await page.clock.pauseAt(Date.now() + 1_000)

    await news.searchFor(wanted.title)

    await expect.poll(() => news.searches).toStrictEqual([wanted.title])
    await page.clock.resume()
    await expect(news.card(wanted.title)).toBeVisible()
    await expect(news.card(other.title)).toBeHidden()
  })

  test('a search without matches says so', async ({ page, seed }) => {
    await seed.newsItem()
    const news = new CardListPage(page, '/news')
    await news.goto()

    await news.searchFor(uniqueTitle('Ninguna novedad'))

    await expect(
      news.main.getByText('No hay novedades que coincidan con tu búsqueda.'),
    ).toBeVisible()
    await expect(news.main.getByRole('heading', { level: 3 })).toHaveCount(0)
  })

  test('a news item page shows the whole news item', async ({ page, seed }) => {
    const item = await seed.newsItem({ text: 'Este sábado abrimos un taller de robótica.' })

    await page.goto(`/news/${item.id}`)

    const article = page.getByRole('main').getByRole('article')
    await expect(article.getByRole('heading', { level: 1, name: item.title })).toBeVisible()
    await expect(article.getByText(item.subtitle, { exact: true })).toBeVisible()
    await expect(article.getByText(dayText(item.createdAt), { exact: true })).toBeVisible()
    await expect(article.getByRole('img', { name: item.title })).toBeVisible()
    await expect(article.getByText('Este sábado abrimos un taller de robótica.')).toBeVisible()
    await page.getByRole('main').getByRole('link', { name: 'Volver a novedades' }).click()
    await expect(page).toHaveURL('/news')
  })

  test('an unknown news item is not found', async ({ page }) => {
    await page.goto('/news/00000000-0000-4000-8000-000000000000')

    await expect(page.getByRole('main').getByText('No hemos encontrado esa novedad.')).toBeVisible()
    await expect(
      page.getByRole('main').getByRole('link', { name: 'Volver a novedades' }),
    ).toBeVisible()
  })
})
