import { changeOwnPhone } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { phoneNumber } from '../../support/data/people'
import { dayFromToday } from '../../support/data/time'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { codeIn, SUBJECTS } from '../../support/mail/mailbox'
import { BlockedDataPage } from '../../support/pages/admin-people/blocked-data-page'
import { fullName } from '../../support/data/people'
import { chooseOption, expectChecked } from '../../support/ui/element-plus'

test.describe('legal archive of blocked data', () => {
  test('every administrator sees the consultations but only the privacy officer opens one', async ({
    pageAs,
    admin,
    privacyOfficer,
  }) => {
    const archive = new BlockedDataPage(await pageAs(admin))
    await archive.open()

    await expect(archive.consultations.root).toBeVisible()
    await expect(archive.openConsultation).toBeHidden()

    const officerArchive = new BlockedDataPage(await pageAs(privacyOfficer))
    await officerArchive.open()

    await expect(officerArchive.openConsultation).toBeEnabled()
  })

  test('the privacy officer opens a consultation, finds a changed account and downloads its history', async ({
    pageAs,
    privacyOfficer,
    accounts,
    mailbox,
  }) => {
    const person = await accounts.actor({ label: 'archived' })
    await changeOwnPhone(person.api, phoneNumber())
    const reference = uniqueTitle('Diligencias')
    const court = t('pages.admin.blockedData.authorities.Court')
    const page = await pageAs(privacyOfficer)
    const archive = new BlockedDataPage(page)
    await archive.open()
    await archive.openConsultation.click()
    const dialog = archive.dialog
    await chooseOption(dialog.getByTestId('blocked-data-dialog-authority'), 'Court')
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
  })
})
