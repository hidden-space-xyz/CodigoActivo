import type { EventResponse } from '@contract/eventResponse'
import type { NewsItemResponse } from '@contract/newsItemResponse'

import { dayRangeText, dayText } from '../../support/data/formats'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AdminShell } from '../../support/pages/admin-shell'
import { HomePage } from '../../support/pages/public/home-page'
import { DataTable } from '../../support/ui/data-table'
import { notification } from '../../support/ui/element-plus'

test.describe('featured content', () => {
  test.describe.configure({ mode: 'serial' })

  test('a featured upcoming event heads the events section of the home page', async ({
    page,
    seed,
  }) => {
    const event = await seed.event()
    await seed.featureEvent(event)
    const home = new HomePage(page)

    await home.goto()

    const featured = home.events.featured
    await expect(featured).toContainText(t('entities.event.featured.badge'))
    await expect(featured.getByTestId('featured-card-title')).toHaveText(event.title)
    await expect(featured).toContainText(`«${event.subtitle}»`)
    await expect(featured).toContainText(dayRangeText(event.eventStartsAt, event.eventEndsAt))
    await expect(featured).toContainText(t('entities.event.status.signupOpen'))
    const poster = featured.getByTestId('featured-card-poster')
    await expect(poster).toBeVisible()
    await expect(poster).toHaveAccessibleName(event.title)
    await expect(home.events.card(event.title)).toHaveCount(0)
    await featured.getByTestId('featured-card-cta').click()
    await expect(page).toHaveURL(`/events/${event.id}`)
  })

  test('a featured news item heads the news section of the home page', async ({ page, seed }) => {
    const item = await seed.newsItem()
    await seed.featureNewsItem(item)
    const home = new HomePage(page)

    await home.goto()

    const featured = home.news.featured
    await expect(featured).toContainText(t('entities.newsItem.featured.badge'))
    await expect(featured.getByTestId('featured-card-title')).toHaveText(item.title)
    await expect(featured).toContainText(item.subtitle)
    await expect(featured).toContainText(dayText(item.createdAt))
    await expect(home.news.card(item.title)).toHaveCount(0)
    await featured.getByTestId('featured-card-cta').click()
    await expect(page).toHaveURL(`/news/${item.id}`)
  })

  test('an administrator features an event from the events table', async ({
    page,
    pageAs,
    admin,
    seed,
  }) => {
    const previous = await seed.event()
    await seed.featureEvent(previous)
    const event = await seed.event()
    const adminPage = await pageAs(admin)
    await new AdminShell(adminPage).open('admin-events')
    const table = new DataTable(adminPage)
    await table.search(event.title)

    await table.rowAction(event.title, 'feature')

    await expect(notification(adminPage, t('pages.admin.events.toasts.featured'))).toBeVisible()
    await expect(table.row(event.title).getByTestId('admin-events-featured')).toHaveText(
      t('pages.admin.events.tag.featured'),
    )
    await table.row(event.title).getByTestId('row-actions-menu').click()
    const feature = table.menuItem('feature')
    await expect(feature).toBeDisabled()
    await expect(feature).toContainText(t('pages.admin.events.alreadyFeatured'))
    const home = new HomePage(page)
    await home.goto()
    await expect(home.events.featured.getByTestId('featured-card-title')).toHaveText(event.title)
    const unfeatured = await admin.api.get<EventResponse>(`/api/events/${previous.id}`)
    expect(unfeatured.featured).toBe(false)
  })

  test('an administrator features a news item from the news table', async ({
    page,
    pageAs,
    admin,
    seed,
  }) => {
    const previous = await seed.newsItem()
    await seed.featureNewsItem(previous)
    const item = await seed.newsItem()
    const adminPage = await pageAs(admin)
    await new AdminShell(adminPage).open('admin-news')
    const table = new DataTable(adminPage)
    await table.search(item.title)

    await table.rowAction(item.title, 'feature')

    await expect(notification(adminPage, t('pages.admin.news.toasts.featured'))).toBeVisible()
    await expect(table.row(item.title).getByTestId('admin-news-featured')).toHaveText(
      t('pages.admin.news.featured'),
    )
    await table.row(item.title).getByTestId('row-actions-menu').click()
    const feature = table.menuItem('feature')
    await expect(feature).toBeDisabled()
    await expect(feature).toContainText(t('pages.admin.news.alreadyFeatured'))
    const home = new HomePage(page)
    await home.goto()
    await expect(home.news.featured.getByTestId('featured-card-title')).toHaveText(item.title)
    const unfeatured = await admin.api.get<NewsItemResponse>(`/api/news/${previous.id}`)
    expect(unfeatured.featured).toBe(false)
  })
})
