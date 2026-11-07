import { cardDate, dayText } from '../../support/data/formats'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { CardListPage } from '../../support/pages/public/card-list-page'

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
    await expect(card.getByTestId('card-date')).toHaveText(
      cardDate(t('entities.newsItem.card.dateLabel'), dayText(item.createdAt)),
    )
    await expect(card).toContainText(item.subtitle)
    const thumbnail = card.getByTestId('list-thumbnail-image')
    await expect(thumbnail).toBeVisible()
    await expect(thumbnail).toHaveAccessibleName(item.title)
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

    await expect(news.main.getByTestId('news-empty')).toHaveText(t('pages.news.noResults'))
    await expect(news.main.getByTestId('news-card')).toHaveCount(0)
  })

  test('a news item page shows the whole news item', async ({ page, seed }) => {
    const text = 'Este sábado abrimos un taller de robótica.'
    const item = await seed.newsItem({ text })

    await page.goto(`/news/${item.id}`)

    const main = page.getByTestId('page-news-detail')
    const article = main.getByTestId('news-detail-article')
    await expect(article.getByTestId('news-detail-title')).toHaveText(item.title)
    await expect(article.getByTestId('news-detail-subtitle')).toHaveText(item.subtitle)
    await expect(article.getByTestId('news-detail-date')).toHaveText(dayText(item.createdAt))
    const poster = article.getByTestId('news-detail-poster')
    await expect(poster).toBeVisible()
    await expect(poster).toHaveAccessibleName(item.title)
    await expect(article.getByTestId('news-detail-body')).toContainText(text)
    await main.getByTestId('news-detail-back').click()
    await expect(page).toHaveURL('/news')
  })

  test('an unknown news item is not found', async ({ page }) => {
    await page.goto('/news/00000000-0000-4000-8000-000000000000')

    const main = page.getByTestId('page-news-detail')
    await expect(main.getByTestId('news-detail-not-found')).toHaveText(
      t('pages.newsDetail.notFound'),
    )
    await expect(main.getByTestId('news-detail-back')).toBeVisible()
  })
})
