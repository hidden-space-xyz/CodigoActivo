import { ROLE_LABELS, STATUS_LABELS } from '../../support/api/catalogs'
import { addMinor } from '../../support/api/my-account'
import { eventOnFreeDay, householdSignups } from '../../support/api/signups'
import { fullName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
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
    const self = fullName(guardian.account)
    const albaName = fullName(alba)
    const brunoName = fullName(bruno)
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const card = await new EventDetailPage(page).openActivity(event, activity)
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
        [guardian.account.id, ROLE_LABELS.volunteer],
        [alba.id, ROLE_LABELS.participant],
        [bruno.id, ROLE_LABELS.participant],
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
    const albaName = fullName(alba)
    const brunoName = fullName(bruno)
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const card = await new EventDetailPage(page).openActivity(event, activity)
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
      { userId: bruno.id, roleName: ROLE_LABELS.volunteer },
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
