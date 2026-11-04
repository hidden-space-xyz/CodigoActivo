import { PublicEvents } from '../../support/api/public'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { cardDate, dayRangeText } from '../../support/data/formats'
import { EventsPage } from '../../support/pages/public/events-page'

test.describe('events page', () => {
  let events: PublicEvents

  test.beforeEach(({ admin, seed }) => {
    events = new PublicEvents(admin.api, seed)
  })

  test.afterEach(async () => {
    await events.dispose()
  })

  test('an upcoming event card shows its date, status and categories', async ({ page, seed }) => {
    const category = await seed.category()
    const event = await seed.event({ startsIn: 3, categories: [category] })
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()

    const card = await eventsPage.revealUpcoming(event.title)

    await expect(card.getByRole('time')).toHaveText(
      cardDate('Fecha', dayRangeText(event.eventStartsAt, event.eventEndsAt)),
    )
    await expect(card).toContainText(`«${event.subtitle}»`)
    await expect(card).toContainText('Inscripción abierta')
    await expect(card).toContainText(category.name)
    await expect(card.getByRole('img', { name: event.title })).toBeVisible()
    await card.click()
    await expect(page).toHaveURL(`/events/${event.id}`)
  })

  test('the upcoming events load 25 at a time', async ({ page, seed }) => {
    await events.many(26, { startsIn: 200, categories: [await seed.category()] })
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()

    await expect(eventsPage.upcomingCards).toHaveCount(25)
    await eventsPage.loadMore.click()

    await expect.poll(() => eventsPage.upcomingCards.count()).toBeGreaterThan(25)
  })

  test('past events are grouped by year, the newest one first', async ({ page }) => {
    const lastYear = await events.past(-400)
    const thisYear = await events.past(-1)
    const newest = thisYear.eventStartsAt.slice(0, 4)
    const older = lastYear.eventStartsAt.slice(0, 4)
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()

    await expect(eventsPage.year(newest)).toHaveAttribute('aria-pressed', 'true')
    await expect(eventsPage.year(older)).toHaveAttribute('aria-pressed', 'false')
    await eventsPage.searchPast(thisYear.title)
    await expect(eventsPage.card(eventsPage.past, thisYear.title)).toBeVisible()

    await eventsPage.searchPast(lastYear.title)
    await expect(
      eventsPage.past.getByText('No hay eventos que coincidan con los filtros.'),
    ).toBeVisible()
    await eventsPage.year(older).click()

    await expect(eventsPage.year(older)).toHaveAttribute('aria-pressed', 'true')
    await expect(eventsPage.year(newest)).toHaveAttribute('aria-pressed', 'false')
    const card = eventsPage.card(eventsPage.past, lastYear.title)
    await expect(card.getByRole('time')).toHaveText(
      cardDate('Fecha', dayRangeText(lastYear.eventStartsAt, lastYear.eventEndsAt)),
    )
    await expect(card).toContainText('Finalizado')
    await expect(card).toHaveAttribute('href', `/events/${lastYear.id}`)
  })

  test('past events can be searched by title', async ({ page }) => {
    const event = await events.past(-20)
    const other = await events.past(-21)
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()

    await eventsPage.searchPast(event.title)

    await expect(eventsPage.card(eventsPage.past, event.title)).toBeVisible()
    await expect(eventsPage.card(eventsPage.past, other.title)).toBeHidden()
  })

  test('the category filter offers the categories of past events and narrows the list', async ({
    page,
    seed,
  }) => {
    const workshops = await seed.category(uniqueTitle('Talleres'))
    const robotics = await seed.category(uniqueTitle('Robótica'))
    const prefix = uniqueTitle('Pasado')
    const workshop = await events.past(-15, { title: `${prefix} taller`, categories: [workshops] })
    const robot = await events.past(-16, { title: `${prefix} robot`, categories: [robotics] })
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()
    await eventsPage.searchPast(prefix)
    await expect(eventsPage.card(eventsPage.past, workshop.title)).toBeVisible()
    await expect(eventsPage.card(eventsPage.past, robot.title)).toBeVisible()

    await eventsPage.filterPastBy(robotics.name)

    await expect(eventsPage.card(eventsPage.past, robot.title)).toBeVisible()
    await expect(eventsPage.card(eventsPage.past, workshop.title)).toBeHidden()
    await expect(eventsPage.card(eventsPage.past, robot.title)).toContainText(robotics.name)
  })

  test('filters that match no past event say so', async ({ page }) => {
    await events.past(-10)
    const eventsPage = new EventsPage(page)
    await eventsPage.goto()

    await eventsPage.searchPast(uniqueTitle('Ningún evento'))

    await expect(
      eventsPage.past.getByText('No hay eventos que coincidan con los filtros.'),
    ).toBeVisible()
    await expect(eventsPage.past.getByRole('link')).toHaveCount(0)
  })
})
