import { addMinor, eventOnFreeDay, householdSignups } from '../../support/api/signups-extra'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard, ROLE_LABELS, STATUS_LABELS } from '../../support/pages/signups/activity-card'
import { HouseholdDialog } from '../../support/pages/signups/household-dialog'
import {
  confirm,
  expectChecked,
  messageBox,
  notification,
  setChecked,
} from '../../support/ui/element-plus'

test.describe('household signup', () => {
  test('a guardian signs the whole family up with a role for each person', async ({
    seed,
    accounts,
    pageAs,
  }) => {
    const guardian = await accounts.actor({ label: 'household' })
    const alba = await addMinor(guardian, 'Alba', 9)
    const bruno = await addMinor(guardian, 'Bruno', 12)
    const self = `${guardian.account.firstName} ${guardian.account.lastName}`
    const albaName = `${alba.firstName} ${alba.lastName}`
    const brunoName = `${bruno.firstName} ${bruno.lastName}`
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    const household = new HouseholdDialog(page)

    await expect(card.signUp).toBeHidden()
    await card.signUpFamily.click()
    await expect(household.root).toContainText(activity.title)
    await expectChecked(household.person(guardian.account.firstName), true)
    await expectChecked(household.person(albaName), true)
    await expectChecked(household.person(brunoName), true)
    await household.expectRoles(albaName, ['participant', 'volunteer'])
    await household.chooseRole(guardian.account.firstName, 'volunteer')
    await household.submit.click()
    await expect(notification(page, t('pages.eventDetail.toast.missingRoleDetail'))).toContainText(
      t('pages.eventDetail.toast.missingRole'),
    )
    await expect(household.root).toBeVisible()

    await household.chooseRole(albaName, 'participant')
    await household.chooseRole(brunoName, 'participant')
    await household.submit.click()

    await expect(household.root).toBeHidden()
    await expect(
      notification(page, t('pages.eventDetail.toast.householdSuccess', { count: 3 })),
    ).toContainText(t('pages.eventDetail.toast.signupSent'))
    await expect(card.member(self, 'volunteer')).toContainText(STATUS_LABELS.requested)
    await expect(card.member(albaName, 'participant')).toContainText(STATUS_LABELS.requested)
    await expect(card.member(brunoName, 'participant')).toContainText(STATUS_LABELS.requested)
    await expect(card.note(t('pages.eventDetail.household.allInscribed'))).toBeVisible()
    await expect(card.signUpAnother).toBeHidden()
    const signups = await householdSignups(guardian.api, event.id)
    expect(signups.map((signup) => [signup.userId, signup.roleName]).sort()).toStrictEqual(
      [
        [guardian.account.id, 'Voluntario'],
        [alba.id, 'Participante'],
        [bruno.id, 'Participante'],
      ].sort(),
    )
  })

  test('a guardian signs part of the family up and withdraws one of them', async ({
    seed,
    accounts,
    pageAs,
  }) => {
    const guardian = await accounts.actor({ label: 'household' })
    const alba = await addMinor(guardian, 'Alba', 9)
    const bruno = await addMinor(guardian, 'Bruno', 12)
    const albaName = `${alba.firstName} ${alba.lastName}`
    const brunoName = `${bruno.firstName} ${bruno.lastName}`
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    const household = new HouseholdDialog(page)

    await card.signUpFamily.click()
    await setChecked(household.person(guardian.account.firstName), false)
    await household.chooseRole(albaName, 'participant')
    await household.chooseRole(brunoName, 'volunteer')
    await household.submit.click()

    await expect(
      notification(page, t('pages.eventDetail.toast.householdSuccess', { count: 2 })),
    ).toBeVisible()
    await expect(card.member(albaName, 'participant')).toBeVisible()
    await expect(card.member(brunoName, 'volunteer')).toBeVisible()
    await expect(card.signUpAnother).toBeVisible()

    await card.withdrawMember(albaName, 'participant').click()
    await expect(messageBox(page)).toContainText(
      t('pages.eventDetail.unassignConfirm.member', { name: albaName, activity: activity.title }),
    )
    await confirm(page)

    await expect(notification(page, t('pages.eventDetail.toast.unassignSuccess'))).toContainText(
      t('pages.eventDetail.toast.unassignSummary'),
    )
    await expect(card.member(albaName, 'participant')).toBeHidden()
    await expect(card.member(brunoName, 'volunteer')).toBeVisible()
    expect(await householdSignups(guardian.api, event.id)).toMatchObject([
      { userId: bruno.id, roleName: 'Voluntario' },
    ])

    await card.signUpAnother.click()
    await expect(household.row(brunoName)).toContainText(
      t('pages.eventDetail.household.alreadyAs', { role: ROLE_LABELS.volunteer }),
    )
    await expect(household.person(brunoName)).toHaveCount(0)
    await expectChecked(household.person(albaName), true)
    await expectChecked(household.person(guardian.account.firstName), true)
    await setChecked(household.person(guardian.account.firstName), false)
    await household.chooseRole(albaName, 'volunteer')
    await household.submit.click()

    await expect(
      notification(page, t('pages.eventDetail.toast.householdSuccess', { count: 1 })),
    ).toBeVisible()
    await expect(card.member(albaName, 'volunteer')).toBeVisible()
    await expect(card.signUpAnother).toBeVisible()
  })
})
