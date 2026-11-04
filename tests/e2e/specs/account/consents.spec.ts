import { consentsFor } from '../../support/api/account'
import { firstDependent } from '../../support/api/accounts'
import { signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { ConsentsSection } from '../../support/pages/account/consents-section'
import { confirm, dialog, notification } from '../../support/ui/element-plus'

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
    const guardianName = `${actor.account.firstName} ${actor.account.lastName}`
    const minorName = `${minor.firstName} ${minor.lastName}`

    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const consents = new ConsentsSection(account)
    const own = consents.consent(document.name, guardianName)
    const minors = consents.consent(document.name, minorName)
    await expect(own).toContainText('Dado')
    await expect(own).toContainText(event.title)
    await expect(own).toContainText(`Decidió ${guardianName} el`)
    await expect(minors).toContainText('Dado')

    await own.getByRole('button', { name: 'Retirar' }).click()
    await expect(dialog(page, 'Retirar un consentimiento')).toContainText(
      `Vas a retirar tu consentimiento «${document.name}» para el evento «${event.title}».`,
    )
    await confirm(page, 'Retirar un consentimiento', 'Retirar')
    await expect(notification(page, 'Consentimiento retirado.')).toBeVisible()
    await expect(own).toContainText('Retirado')
    await expect(own.getByRole('button', { name: 'Retirar' })).toBeHidden()
    await expect(minors).toContainText('Dado')

    await minors.getByRole('button', { name: 'Retirar' }).click()
    await expect(dialog(page, 'Retirar un consentimiento')).toContainText(
      `Si ${minorName} tiene 14 años o más, esta decisión es suya`,
    )
    await confirm(page, 'Retirar un consentimiento', 'Retirar')
    await expect(minors).toContainText('Retirado')

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
