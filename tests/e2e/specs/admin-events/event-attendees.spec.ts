import { signUp } from '../../support/api/signups'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { SUBJECTS } from '../../support/mail/mailbox'
import { AttendeesTab, EventAdminPage } from '../../support/pages/admin-events/event-admin-page'
import { firstDependent } from '../../support/api/accounts'
import { fullName } from '../../support/data/people'
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

    await eventPage.openTab('Asistentes')

    const attendees = new AttendeesTab(page)
    await expect(attendees.count).toHaveText('2 asistentes')
    const adult = attendees.attendee(fullName(participant.account))
    await expect(adult).toContainText(participant.account.email)
    await expect(adult).toContainText('Participante')
    await expect(attendees.signup(fullName(participant.account), activity.title)).toContainText(
      'Solicitada',
    )
    const child = attendees.attendee(fullName(minor))
    await expect(child).toContainText(`Tutor/a: ${fullName(guardian.account)}`)
    await expect(child).toContainText(guardian.account.email)

    await attendees.search.fill(participant.account.lastName)

    await expect(attendees.count).toHaveText('1 asistente')
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
    await eventPage.openTab('Asistentes')
    const attendees = new AttendeesTab(page)
    const name = fullName(participant.account)
    const sentAfter = new Date()

    await attendees.changeStatus(name, activity.title, 'confirmed')

    await expect(notification(page, 'Estado actualizado.')).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText('Confirmada')
    await expect(eventPage.summaryValue('Participante')).toHaveText('1')
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
    await eventPage.openTab('Asistentes')
    const attendees = new AttendeesTab(page)
    const name = fullName(participant.account)

    await attendees.changeRole(name, activity.title, 'volunteer')

    await expect(notification(page, 'Rol actualizado.')).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText('Voluntario')

    await attendees.changeRole(name, activity.title, 'leader')

    await expect(notification(page, 'Este rol no está permitido para este usuario.')).toBeVisible()
    await expect(attendees.signup(name, activity.title)).toContainText('Voluntario')
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
    await eventPage.openTab('Asistentes')
    const attendees = new AttendeesTab(page)
    await expect(attendees.count).toHaveText('3 asistentes')

    await attendees.sendEmail.click()
    const dialog = page.getByRole('dialog', { name: 'Enviar correo' })
    await expect(dialog.getByText(/El mensaje llegará a 2 direcciones de correo/)).toBeVisible()
    await setChecked(dialog.getByRole('radio', { name: 'Aviso' }), true)
    await dialog.getByLabel('Asunto', { exact: true }).fill(subject)
    await dialog.getByLabel('Mensaje', { exact: true }).fill('El taller pasa al aula 2.')
    const sentAfter = new Date()
    await dialog.getByRole('button', { name: 'Enviar', exact: true }).click()
    await confirm(page, '¿Enviar el correo?', 'Enviar')

    await expect(notification(page, /2 correos en cola de envío/)).toBeVisible()
    await expect(
      notification(page, '1 persona sin correo electrónico confirmado se ha omitido.'),
    ).toBeVisible()
    await expect(dialog).toBeHidden()
    for (const account of [participant.account, member.account]) {
      const email = await mailbox.waitFor({ to: account.email, subject, sentAfter })
      expect(email.text).toContain('El taller pasa al aula 2.')
    }
  })
})
