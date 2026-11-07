import { firstDependent } from '../../support/api/accounts'
import { ROLE_LABELS, STATUS_LABELS, USER_TYPE_LABELS } from '../../support/api/catalogs'
import { signUp } from '../../support/api/signups'
import { fullName } from '../../support/data/people'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { SUBJECTS } from '../../support/mail/mailbox'
import { AttendeesTab, EventAdminPage } from '../../support/pages/admin-events/event-admin-page'
import { confirm, notification, setChecked } from '../../support/ui/element-plus'

test.describe('event attendees admin', () => {
  test('the attendees tab lists adults and minors with their guardian and finds them', async ({
    pageAs,
    admin,
    seed,
    participant,
    guardian,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    const minor = firstDependent(guardian.account)
    await signUp(participant.api, activity, participant.account.id)
    await signUp(guardian.api, activity, minor.id)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openTab('attendees')

    const attendees = new AttendeesTab(page)
    await expect(attendees.count).toHaveText(t('pages.admin.eventDetail.attendees.count', { n: 2 }))
    const adult = attendees.attendee(fullName(participant.account))
    await expect(adult).toContainText(participant.account.email)
    await expect(adult).toContainText(USER_TYPE_LABELS.participant)
    await expect(attendees.signup(fullName(participant.account), activity.title)).toContainText(
      STATUS_LABELS.requested,
    )
    const child = attendees.attendee(fullName(minor))
    await expect(child).toContainText(
      t('pages.admin.eventDetail.attendees.guardian', {
        firstName: guardian.account.firstName,
        lastName: guardian.account.lastName,
      }),
    )
    await expect(child).toContainText(guardian.account.email)

    await attendees.search.fill(participant.account.lastName)

    await expect(attendees.count).toHaveText(t('pages.admin.eventDetail.attendees.count', { n: 1 }))
    await expect(adult).toBeVisible()
    await expect(child).toBeHidden()
  })

  test('confirming a signup updates it and emails the person', async ({
    pageAs,
    admin,
    seed,
    participant,
    mailbox,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openTab('attendees')
    const attendees = new AttendeesTab(page)
    const name = fullName(participant.account)
    const sentAfter = new Date()

    await attendees.changeStatus(name, activity.title, 'confirmed')

    await expect(
      notification(page, t('pages.admin.eventDetail.attendees.toast.statusUpdated')),
    ).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText(STATUS_LABELS.confirmed)
    await expect(eventPage.summaryValue(ROLE_LABELS.participant)).toHaveText('1')
    const email = await mailbox.waitFor({
      to: participant.account.email,
      subject: SUBJECTS.signupConfirmed,
      sentAfter,
    })
    expect(email.subject).toBe(`${SUBJECTS.signupConfirmed}: ${activity.title}`)
  })

  test('an administrator changes the role of a signup to one the person may take', async ({
    pageAs,
    admin,
    seed,
    participant,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openTab('attendees')
    const attendees = new AttendeesTab(page)
    const name = fullName(participant.account)

    await attendees.changeRole(name, activity.title, 'volunteer')

    await expect(
      notification(page, t('pages.admin.eventDetail.attendees.toast.roleUpdated')),
    ).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText(ROLE_LABELS.volunteer)

    await attendees.changeRole(name, activity.title, 'leader')

    await expect(notification(page, t('errors.ActivityRoleNotAllowed'))).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText(ROLE_LABELS.volunteer)
  })

  test('the attendees are emailed a notice, skipping those without an address', async ({
    pageAs,
    admin,
    seed,
    participant,
    member,
    guardian,
    mailbox,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    await signUp(member.api, activity, member.account.id)
    await signUp(guardian.api, activity, firstDependent(guardian.account).id)
    const subject = uniqueTitle('Cambio de aula')
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openTab('attendees')
    const attendees = new AttendeesTab(page)
    await expect(attendees.count).toHaveText(t('pages.admin.eventDetail.attendees.count', { n: 3 }))

    await attendees.sendEmail.click()
    const dialog = page.getByTestId('send-email-dialog')
    await expect(dialog.getByTestId('send-email-reach')).toHaveText(
      t('features.sendEmail.recipients', { count: 2 }),
    )
    await setChecked(dialog.getByTestId('send-email-purpose-notice'), true)
    await dialog.getByTestId('send-email-subject').fill(subject)
    await dialog.getByTestId('send-email-body').fill('El taller pasa al aula 2.')
    const sentAfter = new Date()
    await dialog.getByTestId('send-email-send').click()
    await confirm(page)

    await expect(
      notification(page, t('features.sendEmail.toast.queued', { count: 2 })),
    ).toBeVisible()
    await expect(
      notification(page, t('features.sendEmail.toast.skipped', { count: 1 })),
    ).toBeVisible()
    await expect(dialog).toBeHidden()
    for (const account of [participant.account, member.account]) {
      const email = await mailbox.waitFor({ to: account.email, subject, sentAfter })
      expect(email.text).toContain('El taller pasa al aula 2.')
    }
  })
})
