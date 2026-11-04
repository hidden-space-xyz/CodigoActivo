import { changeOwnPhone } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { phoneNumber } from '../../support/data/people'
import { dayFromToday } from '../../support/data/time'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { codeIn, SUBJECTS } from '../../support/mail/mailbox'
import { BlockedDataPage } from '../../support/pages/admin-people/blocked-data-page'
import { fullName } from '../../support/data/people'
import { chooseOption } from '../../support/ui/element-plus'

test.describe('legal archive of blocked data', () => {
  test('every administrator sees the consultations but only the privacy officer opens one', async ({
    pageAs,
    admin,
    privacyOfficer,
  }) => {
    const archive = new BlockedDataPage(await pageAs(admin))
    await archive.open()

    await expect(archive.consultations.root.getByRole('table').first()).toBeVisible()
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
    const page = await pageAs(privacyOfficer)
    const archive = new BlockedDataPage(page)
    await archive.open()
    await archive.openConsultation.click()
    const dialog = archive.dialog
    await chooseOption(dialog.getByLabel('Quién pide los datos'), 'Un juzgado o tribunal')
    await dialog.getByLabel('Referencia de la petición').fill(reference)
    const password = dialog.getByLabel('Tu contraseña')
    await password.fill(`${PASSWORD}-wrong`)
    await dialog.getByRole('button', { name: 'Enviar código' }).click()

    await expect(dialog.getByRole('alert')).toHaveText('La contraseña actual no es correcta.')

    await password.fill(PASSWORD)
    const sentAfter = new Date()
    await dialog.getByRole('button', { name: 'Enviar código' }).click()
    await expect(dialog).toContainText(
      `Te hemos enviado un código a ${privacyOfficer.account.email}.`,
    )
    const message = await mailbox.waitFor({
      to: privacyOfficer.account.email,
      subject: SUBJECTS.blockedDataCode,
      sentAfter,
    })
    await dialog.getByLabel('Código de verificación').fill(codeIn(message))
    await dialog.getByRole('button', { name: 'Confirmar y abrir' }).click()

    await expect(dialog).toBeHidden()
    await expect(archive.panel).toContainText(
      `Para Un juzgado o tribunal · referencia «${reference}»`,
    )
    await expect(archive.openConsultation).toBeDisabled()

    await archive.search(person.account.phone)

    const candidate = archive.candidates.getByRole('radio', {
      name: new RegExp(`${fullName(person.account)}.*Cuenta actual`),
    })
    await expect(candidate).toBeChecked()
    await expect(archive.candidates).toContainText(/Cuentas: [1-9]/)

    const { fileName, extract } = await archive.downloadExtract()

    expect(fileName).toBe(`codigo-activo-extracto-${dayFromToday(0)}.json`)
    expect(extract.referencia).toBe(reference)
    expect(extract.destinatario).toBe('Un juzgado o tribunal')
    expect(extract.cuenta).toBe(person.account.id)
    expect(extract.entradas.map((entry) => entry.tipo)).toContain('Cuentas')
    await expect(archive.consultations.row(reference)).toContainText('Un juzgado o tribunal')
    await expect(archive.consultations.row(reference)).toContainText(
      fullName(privacyOfficer.account),
    )
  })
})
