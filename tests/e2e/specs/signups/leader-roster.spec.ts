import { ageOn, dependentOf, eventOnFreeDay } from '../../support/api/signups-extra'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
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

    await eventPage.tab('attendees').click()

    const roster = new LeaderRoster(page)
    await expect(roster.notice).toContainText(t('pages.eventDetail.leaderRoster.notice.title'))
    const led = roster.activity(activity.title)
    await expect(led.getByTestId('leader-roster-count')).toHaveText(
      t('pages.eventDetail.leaderRoster.attendeesCount', { n: 3 }),
    )
    const leaders = roster.role(led, 'leader')
    await expect(leaders.getByTestId('leader-roster-role-count')).toHaveText(
      t('pages.eventDetail.leaderRoster.peopleCount', { n: 1 }),
    )
    await expect(roster.attendee(leaders, 'users', member.account.lastName)).toBeVisible()
    const participants = roster.role(led, 'participant')
    await expect(participants.getByTestId('leader-roster-role-count')).toHaveText(
      t('pages.eventDetail.leaderRoster.peopleCount', { n: 2 }),
    )

    const adult = roster.attendee(participants, 'users', participant.account.lastName)
    await expect(adult).toContainText(participant.account.firstName)
    await expect(adult).toContainText(t('pages.eventDetail.leaderRoster.adult'))
    const adultEmail = adult.getByTestId('leader-roster-email')
    await expect(adultEmail).toHaveText(participant.account.email)
    await expect(adultEmail).toHaveAttribute('href', `mailto:${participant.account.email}`)
    const adultPhone = adult.getByTestId('leader-roster-phone')
    await expect(adultPhone).toHaveText(participant.account.phone)
    await expect(adultPhone).toHaveAttribute('href', `tel:${participant.account.phone}`)

    const years = ageOn(minor.birthDate, event.eventStartsAt)
    const dependent = roster.attendee(participants, 'dependents', minor.firstName)
    await expect(dependent).toContainText(minor.lastName)
    await expect(dependent).toContainText(
      t('pages.eventDetail.leaderRoster.ageYears', { age: years, count: years }),
    )
    await expect(dependent).toContainText(
      `${guardian.account.firstName} ${guardian.account.lastName}`,
    )
    const dependentEmail = dependent.getByTestId('leader-roster-guardian-email')
    await expect(dependentEmail).toHaveText(guardian.account.email)
    await expect(dependentEmail).toHaveAttribute('href', `mailto:${guardian.account.email}`)
    const dependentPhone = dependent.getByTestId('leader-roster-guardian-phone')
    await expect(dependentPhone).toHaveText(guardian.account.phone)
    await expect(dependentPhone).toHaveAttribute('href', `tel:${guardian.account.phone}`)

    await expect(roster.role(led, 'volunteer')).toBeHidden()
    await expect(led).not.toContainText(admin.account.lastName)
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

    await expect(leaderPage.tab('activities')).toBeVisible()
    await expect(leaderPage.tab('attendees')).toBeHidden()
    await expect(participantPage.tab('activities')).toBeVisible()
    await expect(participantPage.tab('attendees')).toBeHidden()
  })
})
