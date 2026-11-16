import { ROLE_LABELS, STATUS_LABELS } from '../../support/api/catalogs'
import { assignedActivities, eventOnFreeDay, signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

const SIGNUP_SENT = t('pages.eventDetail.toast.signupSuccess')

test.describe('self signup', () => {
  test('a member chooses a role, signs up and withdraws', async ({ seed, member, pageAs }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(member)
    const card = await new EventDetailPage(page).openActivity(event, activity)

    await expect(card.signUp).toBeDisabled()
    await card.expectRoles(['participant', 'volunteer', 'leader'])
    await card.chooseRole('volunteer')
    await expect(card.signUp).toBeEnabled()
    await card.signUp.click()

    await expect(notification(page, SIGNUP_SENT)).toContainText(
      t('pages.eventDetail.toast.signupSent'),
    )
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    await expect(
      card.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.volunteer })),
    ).toBeVisible()
    await expect(card.signUp).toBeHidden()
    expect(await assignedActivities(member.api, event.id)).toMatchObject([
      {
        activityId: activity.id,
        roleType: { name: ROLE_LABELS.volunteer },
        status: { name: STATUS_LABELS.requested },
      },
    ])

    await card.withdraw.click()
    await expect(messageBox(page)).toContainText(
      t('pages.eventDetail.unassignConfirm.self', { activity: activity.title }),
    )
    await confirm(page)

    await expect(notification(page, t('pages.eventDetail.toast.unassignSuccess'))).toContainText(
      t('pages.eventDetail.toast.unassignSummary'),
    )
    await expect(card.status).toBeHidden()
    await expect(card.signUp).toBeVisible()
    expect(await assignedActivities(member.api, event.id)).toStrictEqual([])
  })

  test('a participant is never offered the leader role', async ({ seed, participant, pageAs }) => {
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(participant)
    const card = await new EventDetailPage(page).openActivity(event, activity)

    await card.expectRoles(['participant', 'volunteer'])
    await card.signUpAs('participant')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    await expect(
      card.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.participant })),
    ).toBeVisible()
    await expect(card.withdraw).toBeVisible()
    expect(await assignedActivities(participant.api, event.id)).toMatchObject([
      { activityId: activity.id, roleType: { name: ROLE_LABELS.participant } },
    ])
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
    const card = await new EventDetailPage(page).openActivity(event, second)
    const overlap = page.getByTestId('overlap-dialog')

    await card.signUpAs('volunteer')
    await expect(overlap).toContainText(first.title)
    await overlap.getByTestId('overlap-dialog-cancel').click()

    await expect(overlap).toBeHidden()
    await expect(card.signUp).toBeVisible()
    expect(await assignedActivities(participant.api, event.id)).toMatchObject([
      { activityId: first.id },
    ])

    await card.signUp.click()
    await expect(overlap).toContainText(first.title)
    await overlap.getByTestId('overlap-dialog-confirm').click()

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    await expect(
      card.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.volunteer })),
    ).toBeVisible()
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
    const card = await new EventDetailPage(page).openActivity(event, activity)

    await card.chooseRole('participant')
    await expect(card.root.getByTestId('activity-card-high-demand')).toHaveText(
      t('pages.eventDetail.highDemandWarning'),
    )

    await card.chooseRole('volunteer')
    await expect(card.root.getByTestId('activity-card-high-demand')).toBeHidden()
  })

  test('the person sees whether each signup was confirmed or denied', async ({
    seed,
    member,
    pageAs,
  }) => {
    const event = await eventOnFreeDay(seed)
    const confirmed = await seed.activity(event, { startHour: 10, hours: 1 })
    const denied = await seed.activity(event, { startHour: 12, hours: 1 })
    await seed.enrol(member.api, confirmed, member.account.id, 'participant', 'confirmed')
    await seed.enrol(member.api, denied, member.account.id, 'volunteer', 'denied')

    const page = await pageAs(member)
    const details = new EventDetailPage(page)
    const confirmedCard = await details.openActivity(event, confirmed)
    await expect(confirmedCard.status).toHaveText(STATUS_LABELS.confirmed)
    await expect(
      confirmedCard.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.participant })),
    ).toBeVisible()
    const deniedCard = await details.openActivity(event, denied)
    await expect(deniedCard.status).toHaveText(STATUS_LABELS.denied)
    await expect(
      deniedCard.note(t('pages.eventDetail.card.enrolledAs', { role: ROLE_LABELS.volunteer })),
    ).toBeVisible()
  })
})
