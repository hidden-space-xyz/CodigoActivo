import { findUser } from '../../support/api/accounts'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
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
    await users.act(person, 'sendEmail')
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText(
      t('features.sendEmail.target', { target: fullName(person) }),
    )
    await expect(email.root).toContainText(t('features.sendEmail.recipients', { count: 1 }))

    await email.send.click()
    await expect(email.root.getByTestId('send-email-purpose-error')).toHaveText(
      t('features.sendEmail.form.problems.purposeRequired'),
    )
    await expect(email.root.getByTestId('send-email-subject-error')).toHaveText(
      t('features.sendEmail.form.problems.subjectRequired'),
    )
    await expect(email.root.getByTestId('send-email-body-error')).toHaveText(
      t('features.sendEmail.form.problems.bodyRequired'),
    )

    const subject = uniqueTitle('Aviso')
    const body = `Mañana cambiamos de aula: ${uniqueLetters(10)}.`
    await email.fill({ purpose: 'notice', subject, body, attachments: [ATTACHMENT] })
    const sentAfter = new Date()
    await email.sendConfirmed(t('features.sendEmail.confirm.messageCount', { count: 1 }))

    await expect(
      notification(page, t('features.sendEmail.toast.queued', { count: 1 })),
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
    await users.act(person, 'sendEmail')
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText(t('features.sendEmail.recipients', { count: 1 }))

    await email.choose('news')

    await expect(email.root).toContainText(t('features.sendEmail.newsRecipients', { count: 0 }))
    await expect(email.root.getByTestId('send-email-without-consent')).toContainText(
      t('features.sendEmail.withoutConsent', { count: 1 }),
    )
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
    await users.act(account, 'sendEmail')
    const email = new SendEmailDialog(adminPage)
    await email.choose('news')
    await expect(email.root).toContainText(t('features.sendEmail.newsRecipients', { count: 1 }))

    const subject = uniqueTitle('Novedades')
    await email.fill({ purpose: 'news', subject, body: 'Abrimos un taller de robótica.' })
    const sentAfter = new Date()
    await email.sendConfirmed(t('features.sendEmail.confirm.messageCount', { count: 1 }))
    await expect(
      notification(adminPage, t('features.sendEmail.toast.queued', { count: 1 })),
    ).toBeVisible()

    const message = await mailbox.waitFor({ to: account.email, subject, sentAfter })
    await page.goto(linkIn(message, '/unsubscribe'))

    const unsubscribe = page.getByTestId('page-unsubscribe')
    await expect(unsubscribe.getByTestId('status-panel-title')).toHaveText(
      t('pages.unsubscribe.successTitle'),
    )
    await expect(unsubscribe.getByTestId('status-panel-text')).toHaveText(
      t('pages.unsubscribe.successText'),
    )
    expect((await findUser(admin.api, account.email))?.newsConsent).toBe(false)
  })

  test('an unsubscribe link without its token is refused', async ({ page }) => {
    await page.goto('/unsubscribe')

    const unsubscribe = page.getByTestId('page-unsubscribe')
    await expect(unsubscribe.getByTestId('status-panel-title')).toHaveText(
      t('pages.unsubscribe.errorTitle'),
    )
    await expect(unsubscribe.getByTestId('status-panel-text')).toHaveText(
      t('pages.unsubscribe.incompleteLink'),
    )
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
    await users.table.searchColumn('users-filter-email', `e2e-${label}-`)
    await expect(users.table.count).toHaveText(t('table.results', { n: 2 }))

    await users.bulkEmail.click()
    const email = new SendEmailDialog(page)
    await expect(email.root).toContainText(
      t('features.sendEmail.target', {
        target: t('pages.admin.users.email.targetFiltered', { n: 2 }),
      }),
    )
    await expect(email.root).toContainText(t('features.sendEmail.recipients', { count: 2 }))
    const subject = uniqueTitle('Aviso masivo')
    const body = 'La sede estará cerrada el viernes.'
    await email.fill({ purpose: 'notice', subject, body })
    const sentAfter = new Date()
    await email.sendConfirmed(t('features.sendEmail.confirm.messageCount', { count: 2 }))

    await expect(
      notification(page, t('features.sendEmail.toast.queued', { count: 2 })),
    ).toBeVisible()
    for (const recipient of recipients) {
      const message = await mailbox.waitFor({ to: recipient.email, subject, sentAfter })
      expect(message.text).toContain(body)
    }
  })
})
