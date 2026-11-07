import { firstDependent } from '../../support/api/accounts'
import { consentsFor } from '../../support/api/my-account'
import { signUp } from '../../support/api/signups'
import { fullName } from '../../support/data/people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AccountPage } from '../../support/pages/account/account-page'
import { ConsentsSection } from '../../support/pages/account/consents-section'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

test.describe('consents', () => {
  test('a guardian withdraws the consents given for themselves and for a minor', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'consents', minors: 1 })
    const minor = firstDependent(actor.account)
    const document = await seed.termsDocument({ kind: 'Consent' })
    const event = await seed.event({ terms: [{ document, required: false }] })
    const activity = await seed.activity(event)
    await signUp(actor.api, activity, actor.account.id)
    await signUp(actor.api, activity, minor.id)
    const guardianName = fullName(actor.account)
    const minorName = fullName(minor)

    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const consents = new ConsentsSection(account)
    const own = consents.consent(document.name, guardianName)
    const minors = consents.consent(document.name, minorName)
    await expect(own).toContainText(t('pages.account.consents.given'))
    await expect(own).toContainText(event.title)
    await expect(own).toContainText(
      t('pages.account.consents.decided', { name: guardianName, date: '' }),
    )
    await expect(minors).toContainText(t('pages.account.consents.given'))

    await own.getByTestId('consents-item-withdraw').click()
    await expect(messageBox(page)).toContainText(
      t('pages.account.consents.withdraw.messageSelf', {
        document: document.name,
        event: event.title,
      }),
    )
    await confirm(page)
    await expect(notification(page, t('pages.account.consents.withdraw.done'))).toBeVisible()
    await expect(own).toContainText(t('pages.account.consents.withdrawn'))
    await expect(own.getByTestId('consents-item-withdraw')).toBeHidden()
    await expect(minors).toContainText(t('pages.account.consents.given'))

    await minors.getByTestId('consents-item-withdraw').click()
    await expect(messageBox(page)).toContainText(
      t('pages.account.consents.withdraw.messageMinor', {
        document: document.name,
        person: minorName,
        event: event.title,
      }),
    )
    await confirm(page)
    await expect(minors).toContainText(t('pages.account.consents.withdrawn'))

    const stored = await consentsFor(actor.api, document.id)
    expect(stored).toHaveLength(2)
    expect(stored).toStrictEqual(
      expect.arrayContaining([
        expect.objectContaining({ personId: actor.account.id, accepted: false, withdrawn: true }),
        expect.objectContaining({ personId: minor.id, accepted: false, withdrawn: true }),
      ]),
    )
  })
})
