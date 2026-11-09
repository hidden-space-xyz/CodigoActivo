import { designatePrivacyOfficer } from '../../support/api/accounts'
import { changeOwnPhone } from '../../support/api/my-account'
import { LEGAL_ENTITY, PASSWORD } from '../../support/config/environment'
import { fullName, phoneNumber } from '../../support/data/people'
import { dayFromToday } from '../../support/data/time'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { codeIn, SUBJECTS } from '../../support/mail/mailbox'
import { BlockedDataPage } from '../../support/pages/admin-people/blocked-data-page'
import { UsersAdminPage } from '../../support/pages/admin-people/users-admin-page'
import { AdminShell } from '../../support/pages/admin-shell'
import { confirm, expectChecked, notification } from '../../support/ui/element-plus'

const NOTICE_DELIVERY_MS = 75_000

test.describe('data protection', () => {
  test('the privacy officer, unlike other administrators, reads the audit and opens consultations', async ({
    pageAs,
    admin,
    privacyOfficer,
  }) => {
    const archive = new BlockedDataPage(await pageAs(admin))
    await archive.open()
    await expect(archive.consultations.root).toBeVisible()
    await expect(archive.openConsultation).toBeHidden()

    const officerPage = await pageAs(privacyOfficer)
    const officerArchive = new BlockedDataPage(officerPage)
    await officerArchive.open()
    await expect(officerArchive.openConsultation).toBeEnabled()
    const shell = new AdminShell(officerPage)
    await shell.link('admin-audit').click()
    await expect(officerPage).toHaveURL(AdminShell.path('admin-audit'))
    await expect(shell.view('admin-audit')).toBeVisible()
  })

  test('the privacy officer opens a consultation, downloads the history of an account and holds it', async ({
    pageAs,
    privacyOfficer,
    accounts,
    mailbox,
  }) => {
    test.slow()
    const person = await accounts.actor({ label: 'archived' })
    await changeOwnPhone(person.api, phoneNumber())
    const reference = uniqueTitle('Diligencias')
    const court = 'Juzgado de Primera Instancia n.º 2 de León'
    const page = await pageAs(privacyOfficer)
    const archive = new BlockedDataPage(page)
    await archive.open()
    await archive.openConsultation.click()
    const dialog = archive.dialog
    await dialog.getByTestId('blocked-data-dialog-requester').fill(court)
    await dialog.getByTestId('blocked-data-dialog-reference').fill(reference)
    const password = dialog.getByTestId('blocked-data-dialog-password')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByTestId('blocked-data-dialog-continue').click()

    await expect(dialog.getByTestId('blocked-data-dialog-error')).toHaveText(
      t('errors.UserCurrentPasswordIncorrect'),
    )

    await password.fill(PASSWORD)
    const sentAfter = new Date()
    await dialog.getByTestId('blocked-data-dialog-continue').click()
    await expect(dialog).toContainText(
      t('pages.admin.blockedData.open.codeSent', { email: privacyOfficer.account.email }),
    )
    const message = await mailbox.waitFor({
      to: privacyOfficer.account.email,
      subject: SUBJECTS.blockedDataCode,
      sentAfter,
    })
    await dialog.getByTestId('blocked-data-dialog-code').fill(codeIn(message))
    await dialog.getByTestId('blocked-data-dialog-confirm').click()

    await expect(dialog).toBeHidden()
    await expect(archive.panel).toContainText(court)
    await expect(archive.panel).toContainText(reference)
    await expect(archive.openConsultation).toBeDisabled()
    await mailbox.waitFor(
      { to: LEGAL_ENTITY.email, subject: SUBJECTS.blockedDataConsultation, sentAfter },
      NOTICE_DELIVERY_MS,
    )

    await archive.search(person.account.phone)

    const candidate = archive.candidates
      .getByTestId('blocked-data-panel-candidate')
      .filter({ hasText: fullName(person.account) })
    await expect(candidate).toContainText(t('pages.admin.blockedData.consultation.exists'))
    await expectChecked(candidate, true)
    await expect(archive.candidates).toContainText(
      new RegExp(`${t('pages.admin.blockedData.entities.User')}: [1-9]`),
    )

    const { fileName, extract } = await archive.downloadExtract()

    expect(fileName).toBe(t('pages.admin.blockedData.extract.fileName', { date: dayFromToday(0) }))
    expect(extract.referencia).toBe(reference)
    expect(extract.destinatario).toBe(court)
    expect(extract.cuenta).toBe(person.account.id)
    expect(extract.entradas.map((entry) => entry.tipo)).toContain(
      t('pages.admin.blockedData.entities.User'),
    )
    await expect(archive.consultations.row(reference)).toContainText(court)
    await expect(archive.consultations.row(reference)).toContainText(
      fullName(privacyOfficer.account),
    )

    await archive.panel.getByTestId('blocked-data-panel-hold').click()
    await confirm(page)

    const hold = archive.holds.row(reference)
    await expect(hold).toContainText(t('pages.admin.blockedData.holds.active'))
    await expect(hold).toContainText(court)
    await expect(hold).toContainText(fullName(privacyOfficer.account))

    await archive.holds.rowAction(reference, 'lift')
    await confirm(page)

    await expect(hold).not.toContainText(t('pages.admin.blockedData.holds.active'))
    await expect(hold.getByTestId('row-action-lift')).toBeDisabled()
  })

  test('the superadministrator hands the designation to another administrator', async ({
    pageAs,
    initialAdmin,
    privacyOfficer,
    accounts,
  }) => {
    const successor = await accounts.actor({ label: 'successor', type: 'member', admin: true })
    const page = await pageAs(initialAdmin)
    const users = new UsersAdminPage(page)
    await users.open()
    await users.find(privacyOfficer.account.lastName)
    await expect(
      users.row(privacyOfficer.account).getByTestId('users-privacy-officer'),
    ).toBeVisible()
    await users.find(successor.account.lastName)
    await users.act(successor.account, 'privacyOfficer')
    const dialog = page.getByTestId('privacy-officer-dialog')
    const password = dialog.getByTestId('admin-password-input')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByTestId('form-dialog-save').click()
    await expect(dialog.getByTestId('admin-password-error')).toHaveText(
      t('errors.UserCurrentPasswordIncorrect'),
    )

    try {
      await password.fill(PASSWORD)
      await dialog.getByTestId('form-dialog-save').click()

      await expect(
        notification(page, t('pages.admin.users.toasts.privacyOfficerDesignated')),
      ).toBeVisible()
      await expect(users.row(successor.account).getByTestId('users-privacy-officer')).toBeVisible()
      const successorArchive = new BlockedDataPage(await pageAs(successor))
      await successorArchive.open()
      await expect(successorArchive.openConsultation).toBeEnabled()
      const formerArchive = new BlockedDataPage(await pageAs(privacyOfficer))
      await formerArchive.open()
      await expect(formerArchive.openConsultation).toBeHidden()
    } finally {
      await designatePrivacyOfficer(initialAdmin.api, privacyOfficer.account.id)
    }
  })
})
