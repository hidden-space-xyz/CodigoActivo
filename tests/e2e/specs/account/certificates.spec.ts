import { certificatesFor } from '../../support/api/account'
import { firstDependent } from '../../support/api/accounts'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage, saveDownload } from '../../support/pages/account/account-page'
import {
  CertificateDialog,
  certificatePngName,
  CertificatesTab,
} from '../../support/pages/account/certificates-tab'

const PNG_SIGNATURE = '89504e470d0a1a0a'

test.describe('diplomas', () => {
  test('each person with a confirmed place in an ended event gets a diploma', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'diplomas', minors: 1 })
    const minor = firstDependent(actor.account)
    const ended = await seed.event({ startsIn: -10 })
    const activity = await seed.activity(ended)
    await seed.enrol(activity, actor.account.id)
    await seed.enrol(activity, minor.id)
    const upcoming = await seed.event()
    await seed.enrol(await seed.activity(upcoming), actor.account.id)
    const guardianName = `${actor.account.firstName} ${actor.account.lastName}`
    const minorName = `${minor.firstName} ${minor.lastName}`
    const issued = await certificatesFor(actor.api, ended.id)

    const page = await pageAs(actor)
    await new AccountPage(page).goto('Diplomas')
    const diplomas = new CertificatesTab(page)

    expect(
      issued.map((certificate) => `${certificate.firstName} ${certificate.lastName}`),
    ).toStrictEqual([guardianName, minorName])
    for (const certificate of issued) {
      const name = `${certificate.firstName} ${certificate.lastName}`
      expect(certificate.code).toMatch(/^CA-\d{4}-[0-9A-F]{8}$/)
      await expect(diplomas.tile(name, ended.title)).toContainText(certificate.code)
    }
    await expect(page.getByText(upcoming.title)).toBeHidden()
    expect(await certificatesFor(actor.api, upcoming.id)).toHaveLength(0)

    await diplomas.open(guardianName, ended.title).click()
    const preview = new CertificateDialog(page)
    await expect(preview.preview(ended.title)).toBeVisible()
    const file = await saveDownload(page, () => preview.downloadPng.click())

    expect(file.name).toBe(certificatePngName(ended.title, guardianName))
    expect(file.content.subarray(0, 8).toString('hex')).toBe(PNG_SIGNATURE)
  })
})
