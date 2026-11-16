import { cardDate, dayText } from '../../support/data/formats'
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
})
