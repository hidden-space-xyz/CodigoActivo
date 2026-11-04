import { expect, test } from '../../support/fixtures/test'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'
import {
  dayRangeText,
  momentRangeText,
  momentText,
  timeRangeText,
} from '../../support/data/formats'

const SIGNUP_CLOSED = 'La inscripción no está abierta para este evento.'
const EARLY_SIGNUP_ONLY =
  'Este evento está en periodo de inscripción anticipada, reservado a socios y patrocinadores. La inscripción general abrirá más adelante.'

test.describe('event page', () => {
  test('the information tab describes the event and its signup', async ({ page, seed }) => {
    const category = await seed.category()
    const event = await seed.event({ categories: [category] })
    const detail = new EventDetailPage(page)

    await detail.goto(event.id, event.title)

    await expect(detail.main.getByText(`«${event.subtitle}»`)).toBeVisible()
    await expect(detail.main.getByText(category.name, { exact: true })).toBeVisible()
    await expect(detail.main.getByRole('img', { name: event.title })).toBeVisible()
    await expect(
      detail.main.getByRole('heading', { level: 2, name: 'Sobre el evento' }),
    ).toBeVisible()
    await expect(detail.main.getByText('Una jornada de talleres de programación.')).toBeVisible()
    const panel = detail.main.getByRole('complementary')
    await expect(panel.getByRole('term')).toHaveText(['Fecha', 'Inscripción', 'Estado'])
    await expect(panel.getByRole('definition')).toHaveText([
      dayRangeText(event.eventStartsAt, event.eventEndsAt),
      momentRangeText(event.signupStartsAt, event.signupEndsAt),
      'Inscripción abierta',
    ])
  })

  test('an event with early signup tells when it opens', async ({ page, seed }) => {
    const event = await seed.event({ signup: 'early' })
    const detail = new EventDetailPage(page)

    await detail.goto(event.id, event.title)

    const panel = detail.main.getByRole('complementary')
    await expect(panel.getByRole('term')).toHaveText([
      'Fecha',
      'Inscripción anticipada',
      'Inscripción',
      'Estado',
    ])
    await expect(panel.getByRole('definition')).toHaveText([
      dayRangeText(event.eventStartsAt, event.eventEndsAt),
      momentText(event.earlySignupStartsAt),
      momentRangeText(event.signupStartsAt, event.signupEndsAt),
      'Inscripción anticipada',
    ])
  })

  test('«Ver actividades» switches to the activities tab and back', async ({ page, seed }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    const detail = new EventDetailPage(page)
    await detail.goto(event.id, event.title)

    await detail.main.getByRole('button', { name: 'Ver actividades', exact: true }).click()

    await expect(detail.activity(activity.title)).toBeVisible()
    await expect(detail.main.getByRole('heading', { name: 'Sobre el evento' })).toBeHidden()
    await detail.tab('Información').click()
    await expect(detail.main.getByRole('heading', { name: 'Sobre el evento' })).toBeVisible()
    await expect(detail.activity(activity.title)).toBeHidden()
  })

  test('the activities tab tells when, where and what each activity is', async ({ page, seed }) => {
    const event = await seed.event()
    const onSite = await seed.activity(event, { location: 'Biblioteca de León', startHour: 10 })
    const online = await seed.activity(event, {
      online: true,
      location: 'Videollamada',
      startHour: 17,
      hours: 1,
      description: 'Sesión en directo para toda la familia.',
    })
    const detail = new EventDetailPage(page)
    await detail.goto(event.id, event.title)

    await detail.openActivities()

    await expect(detail.main.getByText(momentText(onSite.activityStartsAt))).toBeVisible()
    await expect(detail.main.getByText(momentText(online.activityStartsAt))).toBeVisible()
    const onSiteCard = detail.activity(onSite.title)
    await expect(onSiteCard).toContainText(
      timeRangeText(onSite.activityStartsAt, onSite.activityEndsAt),
    )
    await expect(onSiteCard).toContainText('Presencial · Biblioteca de León')
    await expect(onSiteCard).toContainText('Taller práctico de la suite e2e.')
    const onlineCard = detail.activity(online.title)
    await expect(onlineCard).toContainText(
      timeRangeText(online.activityStartsAt, online.activityEndsAt),
    )
    await expect(onlineCard).toContainText('Online · Videollamada')
    await expect(onlineCard).toContainText('Sesión en directo para toda la familia.')
  })

  test('a visitor is asked to sign in and comes back to the event', async ({
    page,
    seed,
    accounts,
    mailbox,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    const account = await accounts.create({ label: 'event-login' })
    const detail = new EventDetailPage(page)
    await detail.goto(event.id, event.title)
    await detail.openActivities()

    await detail
      .activity(activity.title)
      .getByRole('button', { name: 'Inicia sesión para apuntarte' })
      .click()

    await expect(page).toHaveURL(`/login?redirect=/events/${event.id}`)
    await expect(new LoginPage(page).heading).toBeVisible()
    await signInThroughPages(page, mailbox, account)
    await expect(page).toHaveURL(`/events/${event.id}`)
    await expect(detail.main.getByRole('heading', { level: 1, name: event.title })).toBeVisible()
    await detail.openActivities()
    await expect(
      detail.activity(activity.title).getByRole('button', { name: 'Inicia sesión para apuntarte' }),
    ).toBeHidden()
  })

  test('a signup that has closed or has not opened yet is announced', async ({ page, seed }) => {
    const detail = new EventDetailPage(page)
    for (const [signup, status] of [
      ['closed', 'Inscripción cerrada'],
      ['upcoming', 'Próximamente'],
    ] as const) {
      const event = await seed.event({ signup })
      await seed.activity(event)
      await detail.goto(event.id, event.title)
      await expect(
        detail.main.getByRole('complementary').getByRole('definition').last(),
      ).toHaveText(status)

      await detail.openActivities()

      await expect(detail.main.getByText(SIGNUP_CLOSED)).toBeVisible()
    }
  })

  test('early signup is announced to visitors and participants', async ({
    page,
    pageAs,
    participant,
    seed,
  }) => {
    const event = await seed.event({ signup: 'early' })
    const activity = await seed.activity(event)
    const visitor = new EventDetailPage(page)
    await visitor.goto(event.id, event.title)
    await visitor.openActivities()
    await expect(visitor.main.getByText(EARLY_SIGNUP_ONLY)).toBeVisible()

    const asParticipant = new EventDetailPage(await pageAs(participant))
    await asParticipant.goto(event.id, event.title)
    await asParticipant.openActivities()

    await expect(asParticipant.main.getByText(EARLY_SIGNUP_ONLY)).toBeVisible()
    await expect(asParticipant.activity(activity.title)).toContainText(
      'Solo socios y patrocinadores por ahora.',
    )
  })

  test('members may already sign up during early signup', async ({ pageAs, member, seed }) => {
    const event = await seed.event({ signup: 'early' })
    const activity = await seed.activity(event)
    const detail = new EventDetailPage(await pageAs(member))
    await detail.goto(event.id, event.title)

    await detail.openActivities()

    await expect(detail.activity(activity.title)).toBeVisible()
    await expect(detail.main.getByText(EARLY_SIGNUP_ONLY)).toBeHidden()
    await expect(detail.main.getByText(SIGNUP_CLOSED)).toBeHidden()
    await expect(
      detail.activity(activity.title).getByRole('button', { name: 'Apuntarme' }),
    ).toBeVisible()
  })

  test('an unknown event is not found', async ({ page }) => {
    await page.goto('/events/00000000-0000-4000-8000-000000000000')

    await expect(page.getByRole('main').getByText('No hemos encontrado ese evento.')).toBeVisible()
    await expect(
      page.getByRole('main').getByRole('link', { name: 'Volver a eventos' }),
    ).toBeVisible()
  })
})
