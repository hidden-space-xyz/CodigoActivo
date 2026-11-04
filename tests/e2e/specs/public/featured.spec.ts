import type { EventResponse } from '@contract/eventResponse'
import type { NewsItemResponse } from '@contract/newsItemResponse'

import { expect, test } from '../../support/fixtures/test'
import { AdminShell } from '../../support/pages/admin-shell'
import { dayRangeText, dayText } from '../../support/data/formats'
import { HomePage } from '../../support/pages/public/home-page'
import { DataTable } from '../../support/ui/data-table'
import { notification } from '../../support/ui/element-plus'

const SEARCH = 'Buscar por título o subtítulo'

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
    await expect(featured).toContainText('★ Evento destacado')
    await expect(featured.getByRole('heading', { level: 2, name: event.title })).toBeVisible()
    await expect(featured).toContainText(`«${event.subtitle}»`)
    await expect(featured).toContainText(dayRangeText(event.eventStartsAt, event.eventEndsAt))
    await expect(featured).toContainText('Inscripción abierta')
    await expect(featured.getByRole('img', { name: event.title })).toBeVisible()
    await expect(home.events.card(event.title)).toHaveCount(0)
    await featured.getByRole('link', { name: 'Ver detalles →' }).click()
    await expect(page).toHaveURL(`/events/${event.id}`)
  })

  test('a featured news item heads the news section of the home page', async ({ page, seed }) => {
    const item = await seed.newsItem()
    await seed.featureNewsItem(item)
    const home = new HomePage(page)

    await home.goto()

    const featured = home.news.featured
    await expect(featured).toContainText('★ Novedad destacada')
    await expect(featured.getByRole('heading', { level: 2, name: item.title })).toBeVisible()
    await expect(featured).toContainText(item.subtitle)
    await expect(featured).toContainText(dayText(item.createdAt))
    await expect(home.news.card(item.title)).toHaveCount(0)
    await featured.getByRole('link', { name: 'Leer más →' }).click()
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
    await new AdminShell(adminPage).open('Eventos')
    const table = new DataTable(adminPage)
    await table.search(SEARCH, event.title)
    const menu = `Más acciones para ${event.title}`

    await table.rowAction(event.title, 'Destacar evento', menu)

    await expect(notification(adminPage, 'Evento destacado.')).toBeVisible()
    await expect(table.row(event.title).getByText('Destacado', { exact: true })).toBeVisible()
    await table.row(event.title).getByRole('button', { name: menu }).click()
    const feature = table.menuItem(/Destacar evento/).filter({ visible: true })
    await expect(feature).toBeDisabled()
    await expect(feature).toContainText('Ya es el evento destacado')
    const home = new HomePage(page)
    await home.goto()
    await expect(
      home.events.featured.getByRole('heading', { level: 2, name: event.title }),
    ).toBeVisible()
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
    await new AdminShell(adminPage).open('Novedades')
    const table = new DataTable(adminPage)
    await table.search(SEARCH, item.title)
    const menu = `Más acciones para ${item.title}`

    await table.rowAction(item.title, 'Destacar en portada', menu)

    await expect(notification(adminPage, 'Destacado actualizado.')).toBeVisible()
    await expect(table.row(item.title).getByText('Destacada', { exact: true })).toBeVisible()
    await table.row(item.title).getByRole('button', { name: menu }).click()
    const feature = table.menuItem(/Destacar en portada/).filter({ visible: true })
    await expect(feature).toBeDisabled()
    await expect(feature).toContainText('Ya es la novedad destacada')
    const home = new HomePage(page)
    await home.goto()
    await expect(
      home.news.featured.getByRole('heading', { level: 2, name: item.title }),
    ).toBeVisible()
    const unfeatured = await admin.api.get<NewsItemResponse>(`/api/news/${previous.id}`)
    expect(unfeatured.featured).toBe(false)
  })
})
