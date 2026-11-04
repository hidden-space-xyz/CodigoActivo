import {
  assignedActivities,
  eventOnFreeDay,
  ongoingEvent,
  startedActivity,
} from '../../support/api/signups-extra'
import { hoursFromNow } from '../../support/data/time'
import { expect, test } from '../../support/fixtures/test'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard } from '../../support/pages/signups/activity-card'
import { notification } from '../../support/ui/element-plus'

const SIGNUP_SENT = 'Hemos recibido tu solicitud. Te avisaremos por correo cuando se revise.'
const CLOSED_BANNER = 'La inscripción no está abierta para este evento.'
const EARLY_BANNER =
  'Este evento está en periodo de inscripción anticipada, reservado a socios y patrocinadores. La inscripción general abrirá más adelante.'

test.describe('signup windows', () => {
  test('nobody can sign up once the signup period has closed', async ({
    seed,
    participant,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed, { signup: 'closed' })
    const activity = await seed.activity(event)
    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openActivities()

    const card = new ActivityCard(eventPage.activity(activity.title))
    await expect(eventPage.main.getByText(CLOSED_BANNER)).toBeVisible()
    await expect(card.root.getByText('La inscripción no está abierta.')).toBeVisible()
    await expect(card.role).toHaveCount(0)
    await expect(card.signUp).toBeHidden()
  })

  test('a member cannot sign up before the signup period opens', async ({
    seed,
    member,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed, { signup: 'upcoming' })
    const activity = await seed.activity(event)
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openActivities()

    const card = new ActivityCard(eventPage.activity(activity.title))
    await expect(eventPage.main.getByText(CLOSED_BANNER)).toBeVisible()
    await expect(card.root.getByText('La inscripción no está abierta.')).toBeVisible()
    await expect(card.signUp).toBeHidden()
  })

  test('early signup is closed to a participant', async ({ seed, participant, pageAs }) => {
    const event = await eventOnFreeDay(seed, { signup: 'early' })
    const activity = await seed.activity(event)
    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openActivities()

    const card = new ActivityCard(eventPage.activity(activity.title))
    await expect(eventPage.main.getByText(EARLY_BANNER)).toBeVisible()
    await expect(card.root.getByText('Solo socios y patrocinadores por ahora.')).toBeVisible()
    await expect(card.signUp).toBeHidden()
  })

  test('a member signs up during early signup', async ({ seed, member, pageAs }) => {
    const event = await eventOnFreeDay(seed, { signup: 'early' })
    const activity = await seed.activity(event)
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await card.signUpAs('Líder')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(eventPage.main.getByText(EARLY_BANNER)).toBeHidden()
    await expect(card.status).toHaveText('Solicitada')
    await expect(card.root.getByText('Tu inscripción: Líder')).toBeVisible()
  })

  test('a sponsor signs up during early signup', async ({ seed, accounts, pageAs }) => {
    const sponsor = await accounts.actor({ label: 'sponsor', type: 'sponsor' })
    const event = await eventOnFreeDay(seed, { signup: 'early' })
    const activity = await seed.activity(event)
    const page = await pageAs(sponsor)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await card.expectRoles(['Participante', 'Voluntario'])
    await card.signUpAs('Voluntario')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText('Solicitada')
    expect(await assignedActivities(sponsor.api, event.id)).toMatchObject([
      { activityId: activity.id, roleType: { name: 'Voluntario' } },
    ])
  })

  test('an activity that has started takes no more signups or withdrawals', async ({
    admin,
    seed,
    participant,
    pageAs,
  }) => {
    const event = await ongoingEvent(seed)
    const toJoin = await seed.activity(event, { dayOfEvent: 2, startHour: 16 })
    await seed.enrol(participant.api, toJoin, participant.account.id, 'participant', 'requested')
    const joined = await seed.reschedule(toJoin, hoursFromNow(-2), hoursFromNow(1))
    const missed = await startedActivity(admin.api, seed, event)
    const later = await seed.activity(event, { dayOfEvent: 2 })
    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openActivities()

    await expect(eventPage.main.getByText(CLOSED_BANNER)).toBeHidden()
    const joinedCard = new ActivityCard(eventPage.activity(joined.title))
    await expect(joinedCard.root.getByText('Tu inscripción: Participante')).toBeVisible()
    await expect(joinedCard.root.getByText('Esta actividad ya ha empezado.')).toBeVisible()
    await expect(joinedCard.withdraw).toBeHidden()
    const missedCard = new ActivityCard(eventPage.activity(missed.title))
    await expect(missedCard.root.getByText('Esta actividad ya ha empezado.')).toBeVisible()
    await expect(missedCard.signUp).toBeHidden()
    const laterCard = new ActivityCard(eventPage.activity(later.title))
    await expect(laterCard.signUp).toBeVisible()
    await expect(laterCard.root.getByText('Esta actividad ya ha empezado.')).toBeHidden()
  })
})
