import {
  assignedActivities,
  eventOnFreeDay,
  ongoingEvent,
  startedActivity,
} from '../../support/api/signups-extra'
import { hoursFromNow } from '../../support/data/time'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard, ROLE_LABELS, STATUS_LABELS } from '../../support/pages/signups/activity-card'
import { notification } from '../../support/ui/element-plus'

const SIGNUP_SENT = t('pages.eventDetail.toast.signupSuccess')
const CLOSED_BANNER = t('pages.eventDetail.activities.signupClosed')
const EARLY_BANNER = t('pages.eventDetail.activities.earlySignupOnly')

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
    await expect(eventPage.main.getByTestId('event-activities-closed')).toHaveText(CLOSED_BANNER)
    await expect(card.note(t('pages.eventDetail.card.signupClosed'))).toBeVisible()
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
    await expect(eventPage.main.getByTestId('event-activities-closed')).toHaveText(CLOSED_BANNER)
    await expect(card.note(t('pages.eventDetail.card.signupClosed'))).toBeVisible()
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
    await expect(eventPage.main.getByTestId('event-activities-closed')).toHaveText(EARLY_BANNER)
    await expect(card.note(t('pages.eventDetail.card.earlySignupOnly'))).toBeVisible()
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

    await card.signUpAs('leader')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(eventPage.main.getByTestId('event-activities-closed')).toBeHidden()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    await expect(
      card.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.leader })),
    ).toBeVisible()
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

    await card.expectRoles(['participant', 'volunteer'])
    await card.signUpAs('volunteer')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
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

    await expect(eventPage.main.getByTestId('event-activities-closed')).toBeHidden()
    const joinedCard = new ActivityCard(eventPage.activity(joined.title))
    await expect(
      joinedCard.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.participant })),
    ).toBeVisible()
    await expect(joinedCard.note(t('pages.eventDetail.card.activityStarted'))).toBeVisible()
    await expect(joinedCard.withdraw).toBeHidden()
    const missedCard = new ActivityCard(eventPage.activity(missed.title))
    await expect(missedCard.note(t('pages.eventDetail.card.activityStarted'))).toBeVisible()
    await expect(missedCard.signUp).toBeHidden()
    const laterCard = new ActivityCard(eventPage.activity(later.title))
    await expect(laterCard.signUp).toBeVisible()
    await expect(laterCard.note(t('pages.eventDetail.card.activityStarted'))).toBeHidden()
  })
})
