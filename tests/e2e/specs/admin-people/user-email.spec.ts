import { findUser } from '../../support/api/accounts'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { linkIn } from '../../support/mail/mailbox'
import { SendEmailDialog } from '../../support/pages/admin-people/send-email-dialog'
import { fullName } from '../../support/data/people'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'
import { notification } from '../../support/ui/element-plus'

const ATTACHMENT = {
  name: 'programa-e2e.txt',
  mimeType: 'text/plain',
  buffer: Buffer.from('Programa de la jornada de la suite e2e.'),
}

test.describe('emailing users from the admin list', () => {
  test('a notice with an attachment reaches the one user it was written to', async ({
    pageAs,
    admin,
    participant,
    mailbox,
  }) => {
    const person = participant.account
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.lastName)
    await users.act(person, 'Escribir un correo')
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText(`Destinatarios: ${fullName(person)}`)
    await expect(email.root).toContainText('El mensaje llegará a 1 dirección de correo.')

    await email.send.click()
    await expect(email.root.getByText('Elige si el correo es un aviso o novedades.')).toBeVisible()
    await expect(email.root.getByText('Escribe un asunto.')).toBeVisible()
    await expect(email.root.getByText('Escribe un mensaje.')).toBeVisible()

    const subject = uniqueTitle('Aviso')
    const body = `Mañana cambiamos de aula: ${uniqueLetters(10)}.`
    await email.fill({ purpose: 'Aviso', subject, body, attachments: [ATTACHMENT] })
    const sentAfter = new Date()
    await email.sendConfirmed('El correo se enviará a 1 dirección.')

    await expect(
      notification(page, '1 correo en cola de envío. Se entregará en unos instantes.'),
    ).toBeVisible()
    await expect(email.root).toBeHidden()
    const message = await mailbox.waitFor({ to: person.email, subject, sentAfter })
    expect(message.subject).toBe(subject)
    expect(message.text).toContain(body)
    expect(message.attachments).toStrictEqual([ATTACHMENT.name])
  })

  test('news cannot be sent to someone who did not accept receiving it', async ({
    pageAs,
    admin,
    participant,
  }) => {
    const person = participant.account
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(person.lastName)
    await users.act(person, 'Escribir un correo')
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText('El mensaje llegará a 1 dirección de correo.')

    await email.choose('Novedades')

    await expect(email.root).toContainText(
      'Nadie de esta selección ha aceptado recibir novedades, así que no se puede enviar este correo.',
    )
    await expect(
      email.root.getByText(
        '1 destinatario no ha aceptado recibir novedades y no recibirá este correo.',
      ),
    ).toBeVisible()
    await expect(email.send).toBeDisabled()
  })

  test('news carries a link that unsubscribes the person from it', async ({
    page,
    pageAs,
    admin,
    accounts,
    mailbox,
  }) => {
    const account = await accounts.create({ label: 'news', newsConsent: true })
    const adminPage = await pageAs(admin)
    const users = new UsersAdminPage(adminPage)
    await users.open()
    await users.find(account.lastName)
    await users.act(account, 'Escribir un correo')
    const email = new SendEmailDialog(adminPage)
    await email.choose('Novedades')
    await expect(email.root).toContainText(
      'El correo de novedades llegará a 1 dirección de correo.',
    )

    const subject = uniqueTitle('Novedades')
    await email.fill({ purpose: 'Novedades', subject, body: 'Abrimos un taller de robótica.' })
    const sentAfter = new Date()
    await email.sendConfirmed('El correo se enviará a 1 dirección.')
    await expect(
      notification(adminPage, '1 correo en cola de envío. Se entregará en unos instantes.'),
    ).toBeVisible()

    const message = await mailbox.waitFor({ to: account.email, subject, sentAfter })
    await page.goto(linkIn(message, '/unsubscribe'))

    await expect(page.getByRole('heading', { name: 'Te has dado de baja' })).toBeVisible()
    await expect(
      page.getByText('Ya no te enviaremos novedades sobre las actividades de la asociación.'),
    ).toBeVisible()
    expect((await findUser(admin.api, account.email))?.newsConsent).toBe(false)
  })

  test('an unsubscribe link without its token is refused', async ({ page }) => {
    await page.goto('/unsubscribe')

    await expect(page.getByRole('heading', { name: 'No hemos podido darte de baja' })).toBeVisible()
    await expect(
      page.getByText('El enlace para darte de baja no es válido o está incompleto.'),
    ).toBeVisible()
  })

  test('the toolbar emails every user the filters leave in the list', async ({
    pageAs,
    admin,
    accounts,
    mailbox,
  }) => {
    const label = `bulk-${uniqueLetters()}`
    const recipients = [await accounts.create({ label }), await accounts.create({ label })]
    const page = await pageAs(admin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.table.searchColumn('Correo', `e2e-${label}-`)
    await expect(users.table.count).toHaveText('2 resultados')

    await users.bulkEmail.click()
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText(
      'Destinatarios: los 2 usuarios que coinciden con los filtros actuales',
    )
    await expect(email.root).toContainText('El mensaje llegará a 2 direcciones de correo')
    const subject = uniqueTitle('Aviso masivo')
    const body = 'La sede estará cerrada el viernes.'
    await email.fill({ purpose: 'Aviso', subject, body })
    const sentAfter = new Date()
    await email.sendConfirmed('El correo se enviará a 2 direcciones.')

    await expect(
      notification(page, '2 correos en cola de envío. Se entregarán en unos instantes.'),
    ).toBeVisible()
    for (const recipient of recipients) {
      const message = await mailbox.waitFor({ to: recipient.email, subject, sentAfter })
      expect(message.text).toContain(body)
    }
  })
})
