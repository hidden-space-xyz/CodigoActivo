import { ageOn, dependentOf, eventOnFreeDay } from '../../support/api/signups-extra'
import { expect, test } from '../../support/fixtures/test'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { LeaderRoster } from '../../support/pages/signups/leader-roster'

test.describe('leader roster', () => {
  test('a confirmed leader sees the confirmed attendees of the activity they lead', async ({
    seed,
    admin,
    member,
    participant,
    guardian,
    pageAs,
  }) => {
    const minor = dependentOf(guardian.account)
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    await seed.enrol(member.api, activity, member.account.id, 'leader', 'confirmed')
    await seed.enrol(participant.api, activity, participant.account.id, 'participant', 'confirmed')
    await seed.enrol(guardian.api, activity, minor.id, 'participant', 'confirmed')
    await seed.enrol(admin.api, activity, admin.account.id, 'volunteer', 'requested')
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.tab('Asistentes').click()

    const roster = new LeaderRoster(page)
    await expect(roster.notice).toContainText('Datos personales confidenciales')
    const led = roster.activity(activity.title)
    await expect(led.getByText('3 asistentes confirmados')).toBeVisible()
    const leaders = roster.role(led, 'Líder')
    await expect(leaders.getByRole('heading', { level: 3 })).toContainText('1 persona')
    await expect(roster.attendee(leaders, 'Usuarios', member.account.lastName)).toBeVisible()
    const participants = roster.role(led, 'Participante')
    await expect(participants.getByRole('heading', { level: 3 })).toContainText('2 personas')

    const adult = roster.attendee(participants, 'Usuarios', participant.account.lastName)
    await expect(adult).toContainText(participant.account.firstName)
    await expect(adult).toContainText('Adulto/a')
    await expect(adult.getByRole('link', { name: participant.account.email })).toHaveAttribute(
      'href',
      `mailto:${participant.account.email}`,
    )
    await expect(adult.getByRole('link', { name: participant.account.phone })).toHaveAttribute(
      'href',
      `tel:${participant.account.phone}`,
    )

    const dependent = roster.attendee(participants, 'Dependientes', minor.firstName)
    await expect(dependent).toContainText(minor.lastName)
    await expect(dependent).toContainText(`${ageOn(minor.birthDate, event.eventStartsAt)} años`)
    await expect(dependent).toContainText(
      `${guardian.account.firstName} ${guardian.account.lastName}`,
    )
    await expect(dependent.getByRole('link', { name: guardian.account.email })).toHaveAttribute(
      'href',
      `mailto:${guardian.account.email}`,
    )
    await expect(dependent.getByRole('link', { name: guardian.account.phone })).toHaveAttribute(
      'href',
      `tel:${guardian.account.phone}`,
    )

    await expect(roster.role(led, 'Voluntario')).toBeHidden()
    await expect(led.getByText(admin.account.lastName)).toBeHidden()
  })

  test('only a confirmed leader is offered the attendees tab', async ({
    seed,
    member,
    participant,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    await seed.enrol(member.api, activity, member.account.id, 'leader', 'requested')
    await seed.enrol(participant.api, activity, participant.account.id, 'participant', 'confirmed')

    const leaderPage = new EventDetailPage(await pageAs(member))
    await leaderPage.gotoSignedIn(event.id, event.title)
    const participantPage = new EventDetailPage(await pageAs(participant))
    await participantPage.gotoSignedIn(event.id, event.title)

    await expect(leaderPage.tab('Actividades')).toBeVisible()
    await expect(leaderPage.tab('Asistentes')).toBeHidden()
    await expect(participantPage.tab('Actividades')).toBeVisible()
    await expect(participantPage.tab('Asistentes')).toBeHidden()
  })
})
