import { firstDependent } from '../../support/api/accounts'
import { certificatesFor } from '../../support/api/my-account'
import { fullName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import {
  CertificateDialog,
  certificatePngName,
  CertificatesTab,
} from '../../support/pages/account/certificates-tab'
import { saveDownload } from '../../support/ui/download'

const PNG_SIGNATURE = '89504e470d0a1a0a'

test.describe('diplomas', () => {
  test('each person with a confirmed place in an ended event gets a diploma', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'diplomas', minors: 1 })
    const minor = firstDependent(actor.account)
    const signedUp = await seed.event()
    const activity = await seed.activity(signedUp)
    await seed.enrol(actor.api, activity, actor.account.id)
    await seed.enrol(actor.api, activity, minor.id)
    const ended = await seed.moveToPast(signedUp, [activity], 10)
    const upcoming = await seed.event()
    await seed.enrol(actor.api, await seed.activity(upcoming), actor.account.id)
    const guardianName = fullName(actor.account)
    const minorName = fullName(minor)
    const issued = await certificatesFor(actor.api, ended.id)

    const page = await pageAs(actor)
    await new AccountPage(page).goto('certificates')
    const diplomas = new CertificatesTab(page)

    expect(issued.map(fullName)).toStrictEqual([guardianName, minorName])
    for (const certificate of issued) {
      expect(certificate.code).toMatch(/^CA-\d{4}-[0-9A-F]{8}$/)
      await expect(diplomas.tile(fullName(certificate), ended.title)).toContainText(
        certificate.code,
      )
    }
    await expect(
      page.getByTestId('certificate-tile').filter({ hasText: upcoming.title }),
    ).toBeHidden()
    expect(await certificatesFor(actor.api, upcoming.id)).toHaveLength(0)

    await diplomas.open(guardianName, ended.title).click()
    const preview = new CertificateDialog(page)
    await expect(preview.preview(ended.title)).toBeVisible()
    const file = await saveDownload(page, () => preview.downloadPng.click())

    expect(file.name).toBe(certificatePngName(ended.title, guardianName))
    expect(file.content.subarray(0, 8).toString('hex')).toBe(PNG_SIGNATURE)
  })
})
