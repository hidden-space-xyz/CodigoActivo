import { signUp } from '../../support/api/signups'
import { assignedActivities, eventOnFreeDay } from '../../support/api/signups-extra'
import { expect, test } from '../../support/fixtures/test'
import { SUBJECTS } from '../../support/mail/mailbox'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard } from '../../support/pages/signups/activity-card'
import { confirm, dialog, notification } from '../../support/ui/element-plus'

const SIGNUP_SENT = 'Hemos recibido tu solicitud. Te avisaremos por correo cuando se revise.'
const HIGH_DEMAND = /Esta actividad está muy solicitada y es posible que se agoten las plazas/

test.describe('self signup', () => {
  test('a member chooses a role, signs up and withdraws', async ({ seed, member, pageAs }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await expect(card.signUp).toBeDisabled()
    await card.expectRoles(['Participante', 'Voluntario', 'Líder'])
    await card.chooseRole('Voluntario')
    await expect(card.signUp).toBeEnabled()
    await card.signUp.click()

    await expect(notification(page, SIGNUP_SENT)).toContainText('Inscripción enviada')
    await expect(card.status).toHaveText('Solicitada')
    await expect(card.root.getByText('Tu inscripción: Voluntario')).toBeVisible()
    await expect(card.signUp).toBeHidden()
    expect(await assignedActivities(member.api, event.id)).toMatchObject([
      { activityId: activity.id, roleType: { name: 'Voluntario' }, status: { name: 'Solicitada' } },
    ])

    await card.withdraw.click()
    await expect(dialog(page, 'Anular inscripción')).toContainText(
      `¿Quieres anular tu inscripción en «${activity.title}»?`,
    )
    await confirm(page, 'Anular inscripción', 'Anular inscripción')

    await expect(notification(page, 'Se ha eliminado la inscripción.')).toContainText(
      'Inscripción cancelada',
    )
    await expect(card.status).toBeHidden()
    await expect(card.signUp).toBeVisible()
    expect(await assignedActivities(member.api, event.id)).toStrictEqual([])
  })

  test('a participant is never offered the leader role', async ({ seed, participant, pageAs }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await card.expectRoles(['Participante', 'Voluntario'])
    await card.signUpAs('Participante')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText('Solicitada')
    await expect(card.root.getByText('Tu inscripción: Participante')).toBeVisible()
    await expect(card.withdraw).toBeVisible()
    expect(await assignedActivities(participant.api, event.id)).toMatchObject([
      { activityId: activity.id, roleType: { name: 'Participante' } },
    ])
  })

  test('a visitor is asked to sign in before signing up', async ({ page, seed }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await expect(card.role).toHaveCount(0)
    await expect(card.signUp).toBeHidden()
    await card.login.click()

    await expect(page).toHaveURL(`/login?redirect=/events/${event.id}`)
  })

  test('signing up for an activity that overlaps another one asks first', async ({
    seed,
    participant,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed)
    const first = await seed.activity(event, { startHour: 10, hours: 2 })
    const second = await seed.activity(event, { startHour: 11, hours: 2 })
    await signUp(participant.api, first, participant.account.id)
    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(second.title))
    const overlap = dialog(page, 'Coincidencia de horario')

    await card.signUpAs('Voluntario')
    await expect(overlap).toContainText(first.title)
    await overlap.getByRole('button', { name: 'Cancelar' }).click()

    await expect(overlap).toBeHidden()
    await expect(card.signUp).toBeVisible()
    expect(await assignedActivities(participant.api, event.id)).toMatchObject([
      { activityId: first.id },
    ])

    await card.signUp.click()
    await expect(overlap).toContainText(first.title)
    await overlap.getByRole('button', { name: 'Apuntarme igualmente' }).click()

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText('Solicitada')
    await expect(card.root.getByText('Tu inscripción: Voluntario')).toBeVisible()
    const signups = await assignedActivities(participant.api, event.id)
    expect(signups.map((signup) => signup.activityId).sort()).toStrictEqual(
      [first.id, second.id].sort(),
    )
  })

  test('a role in high demand warns the person who chooses it', async ({
    seed,
    member,
    participant,
    guardian,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event, { roles: { participant: 1, volunteer: 3 } })
    await seed.enrol(participant.api, activity, participant.account.id, 'participant', 'requested')
    await seed.enrol(guardian.api, activity, guardian.account.id, 'participant', 'confirmed')
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))

    await card.chooseRole('Participante')
    await expect(card.root.getByText(HIGH_DEMAND)).toBeVisible()

    await card.chooseRole('Voluntario')
    await expect(card.root.getByText(HIGH_DEMAND)).toBeHidden()
  })

  test('a confirmed signup shows as confirmed and the person is emailed', async ({
    seed,
    participant,
    pageAs,
    mailbox,
  }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    const sentAfter = new Date()

    await seed.decide(activity, participant.account.id, 'confirmed')

    const page = await pageAs(participant)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    await expect(card.status).toHaveText('Confirmada')
    await expect(card.root.getByText('Tu inscripción: Participante')).toBeVisible()
    const email = await mailbox.waitFor({
      to: participant.account.email,
      subject: SUBJECTS.signupConfirmed,
      sentAfter,
    })
    expect(email.subject).toBe(`${SUBJECTS.signupConfirmed}: ${activity.title}`)
  })

  test('a denied signup shows as denied and the person is emailed', async ({
    seed,
    member,
    pageAs,
    mailbox,
  }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    await signUp(member.api, activity, member.account.id, 'volunteer')
    const sentAfter = new Date()

    await seed.decide(activity, member.account.id, 'denied')

    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    await expect(card.status).toHaveText('Rechazada')
    await expect(card.root.getByText('Tu inscripción: Voluntario')).toBeVisible()
    const email = await mailbox.waitFor({
      to: member.account.email,
      subject: SUBJECTS.signupDenied,
      sentAfter,
    })
    expect(email.subject).toBe(`${SUBJECTS.signupDenied}: ${activity.title}`)
  })
})
