import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { LoginPage, signInThroughPages } from '../../support/pages/login-page'
import {
  dayRangeText,
  momentRangeText,
  momentText,
  timeRangeText,
} from '../../support/data/formats'

const SIGNUP_CLOSED = t('pages.eventDetail.activities.signupClosed')
const EARLY_SIGNUP_ONLY = t('pages.eventDetail.activities.earlySignupOnly')
const DESCRIPTION = 'Una jornada de talleres de programación.'

test.describe('event page', () => {
  test('the information tab describes the event and its signup', async ({ page, seed }) => {
    const category = await seed.category()
    const event = await seed.event({ categories: [category], description: DESCRIPTION })
    const detail = new EventDetailPage(page)

    await detail.goto(event.id, event.title)

    await expect(detail.main.getByTestId('event-detail-slogan')).toHaveText(
      t('pages.eventDetail.slogan', { subtitle: event.subtitle }),
    )
    await expect(
      detail.main.getByTestId('event-detail-category').filter({ hasText: category.name }),
    ).toBeVisible()
    const poster = detail.main.getByTestId('event-detail-poster')
    await expect(poster).toBeVisible()
    await expect(poster).toHaveAccessibleName(event.title)
    await expect(detail.main.getByTestId('event-detail-about')).toHaveText(
      t('pages.eventDetail.aboutEvent'),
    )
    await expect(detail.main.getByTestId('event-detail-description')).toHaveText(DESCRIPTION)
    const panel = detail.main.getByTestId('event-detail-info')
    await expect(panel.getByTestId('event-detail-info-label')).toHaveText([
      t('pages.eventDetail.info.date'),
      t('pages.eventDetail.info.signup'),
      t('common.status'),
    ])
    await expect(panel.getByTestId('event-detail-info-value')).toHaveText([
      dayRangeText(event.eventStartsAt, event.eventEndsAt),
      momentRangeText(event.signupStartsAt, event.signupEndsAt),
      t('entities.event.status.signupOpen'),
    ])
  })

  test('an event with early signup tells when it opens', async ({ page, seed }) => {
    const event = await seed.event({ signup: 'early' })
    const detail = new EventDetailPage(page)

    await detail.goto(event.id, event.title)

    const panel = detail.main.getByTestId('event-detail-info')
    await expect(panel.getByTestId('event-detail-info-label')).toHaveText([
      t('pages.eventDetail.info.date'),
      t('pages.eventDetail.info.earlySignup'),
      t('pages.eventDetail.info.signup'),
      t('common.status'),
    ])
    await expect(panel.getByTestId('event-detail-info-value')).toHaveText([
      dayRangeText(event.eventStartsAt, event.eventEndsAt),
      momentText(event.earlySignupStartsAt),
      momentRangeText(event.signupStartsAt, event.signupEndsAt),
      t('entities.event.status.earlySignupOpen'),
    ])
  })

  test('«Ver actividades» switches to the activities tab and back', async ({ page, seed }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    const detail = new EventDetailPage(page)
    await detail.goto(event.id, event.title)

    await detail.main.getByTestId('event-detail-view-activities').click()

    await expect(detail.activity(activity.title)).toBeVisible()
    await expect(detail.main.getByTestId('event-detail-about')).toBeHidden()
    await detail.tab('info').click()
    await expect(detail.main.getByTestId('event-detail-about')).toBeVisible()
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

    const times = detail.main.getByTestId('event-activities-time')
    await expect(times.filter({ hasText: momentText(onSite.activityStartsAt) })).toBeVisible()
    await expect(times.filter({ hasText: momentText(online.activityStartsAt) })).toBeVisible()
    const onSiteCard = detail.activity(onSite.title)
    await expect(onSiteCard).toContainText(
      timeRangeText(onSite.activityStartsAt, onSite.activityEndsAt),
    )
    await expect(onSiteCard).toContainText(`${onSite.modalityName} · ${onSite.location}`)
    await expect(onSiteCard).toContainText(onSite.description)
    const onlineCard = detail.activity(online.title)
    await expect(onlineCard).toContainText(
      timeRangeText(online.activityStartsAt, online.activityEndsAt),
    )
    await expect(onlineCard).toContainText(`${online.modalityName} · ${online.location}`)
    await expect(onlineCard).toContainText(online.description)
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

    await detail.activity(activity.title).getByTestId('activity-card-login').click()

    await expect(page).toHaveURL(`/login?redirect=/events/${event.id}`)
    await expect(new LoginPage(page).heading).toBeVisible()
    await signInThroughPages(page, mailbox, account)
    await expect(page).toHaveURL(`/events/${event.id}`)
    await expect(detail.main.getByTestId('event-detail-title')).toHaveText(event.title)
    await detail.openActivities()
    await expect(detail.activity(activity.title).getByTestId('activity-card-login')).toBeHidden()
  })

  test('a signup that has closed or has not opened yet is announced', async ({ page, seed }) => {
    const detail = new EventDetailPage(page)
    for (const [signup, status] of [
      ['closed', t('entities.event.status.signupClosed')],
      ['upcoming', t('entities.event.status.upcoming')],
    ] as const) {
      const event = await seed.event({ signup })
      await seed.activity(event)
      await detail.goto(event.id, event.title)
      await expect(
        detail.main.getByTestId('event-detail-info').getByTestId('event-detail-info-value').last(),
      ).toHaveText(status)

      await detail.openActivities()

      await expect(detail.main.getByTestId('event-activities-closed')).toHaveText(SIGNUP_CLOSED)
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
    await expect(visitor.main.getByTestId('event-activities-closed')).toHaveText(EARLY_SIGNUP_ONLY)

    const asParticipant = new EventDetailPage(await pageAs(participant))
    await asParticipant.goto(event.id, event.title)
    await asParticipant.openActivities()

    await expect(asParticipant.main.getByTestId('event-activities-closed')).toHaveText(
      EARLY_SIGNUP_ONLY,
    )
    await expect(asParticipant.activity(activity.title)).toContainText(
      t('pages.eventDetail.card.earlySignupOnly'),
    )
  })

  test('members may already sign up during early signup', async ({ pageAs, member, seed }) => {
    const event = await seed.event({ signup: 'early' })
    const activity = await seed.activity(event)
    const detail = new EventDetailPage(await pageAs(member))
    await detail.goto(event.id, event.title)

    await detail.openActivities()

    await expect(detail.activity(activity.title)).toBeVisible()
    await expect(detail.main.getByTestId('event-activities-closed')).toBeHidden()
    await expect(detail.activity(activity.title).getByTestId('activity-card-sign-up')).toBeVisible()
  })

  test('an unknown event is not found', async ({ page }) => {
    await page.goto('/events/00000000-0000-4000-8000-000000000000')

    const main = page.getByTestId('page-event-detail')
    await expect(main.getByTestId('event-detail-not-found')).toHaveText(
      t('pages.eventDetail.notFound'),
    )
    await expect(main.getByTestId('event-detail-back')).toHaveText(
      t('pages.eventDetail.backToEvents'),
    )
  })
})
