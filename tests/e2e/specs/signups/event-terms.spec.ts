import { STATUS_LABELS } from '../../support/api/catalogs'
import {
  assignedActivities,
  decisionsOf,
  eventOnFreeDay,
  signUp,
  termsState,
} from '../../support/api/signups'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { TermsDialog } from '../../support/pages/signups/terms-dialog'
import { expectChecked, notification, setChecked } from '../../support/ui/element-plus'

const SIGNUP_SENT = t('pages.eventDetail.toast.signupSuccess')
const CONSENT_TEXT = 'Autorizo el uso de mi imagen en las fotos de la jornada.'

test.describe('event terms', () => {
  test('signing up asks for the required documents and a signature, and stores the decisions', async ({
    seed,
    participant,
    pageAs,
  }) => {
    const conditions = await seed.termsDocument({ kind: 'Conditions', requiresSignature: true })
    const consent = await seed.termsDocument({
      kind: 'Consent',
      name: uniqueTitle('Consentimiento de imagen'),
      text: CONSENT_TEXT,
    })
    const event = await eventOnFreeDay(seed, {
      terms: [
        { document: conditions, required: true },
        { document: consent, required: false },
      ],
    })
    const activity = await seed.activity(event)
    const page = await pageAs(participant)
    const card = await new EventDetailPage(page).openActivity(event, activity)
    const terms = new TermsDialog(page)

    await card.signUpAs('participant')
    await expect(terms.root).toBeVisible()
    await expectChecked(terms.accept(conditions.name), false)
    await expectChecked(terms.accept(consent.name), false)
    await expect(terms.signature(conditions.name)).toBeHidden()
    await terms.confirm.click()
    await expect(terms.root.getByTestId('terms-dialog-missing-required')).toHaveText(
      t('pages.eventDetail.terms.missingRequired'),
    )

    await setChecked(terms.accept(conditions.name), true)
    await expect(terms.root.getByTestId('terms-dialog-missing-required')).toBeHidden()
    await expect(terms.signature(conditions.name)).toBeVisible()
    await expect(terms.clearSignature(conditions.name)).toBeDisabled()
    await terms.confirm.click()
    await expect(terms.root.getByTestId('terms-dialog-missing-signature')).toHaveText(
      t('pages.eventDetail.terms.signature.missing'),
    )

    await terms.sign(conditions.name)
    await expect(terms.clearSignature(conditions.name)).toBeEnabled()
    await expect(terms.root.getByTestId('terms-dialog-missing-signature')).toBeHidden()

    await terms.open(consent.name).click()
    const preview = terms.preview(consent.name)
    await expect(preview).toContainText(CONSENT_TEXT)
    await preview.getByTestId('terms-dialog-preview-accept').click()
    await expect(preview).toBeHidden()
    await expectChecked(terms.accept(consent.name), true)
    await terms.open(consent.name).click()
    await preview.getByTestId('terms-dialog-preview-reject').click()
    await expect(preview).toBeHidden()
    await expectChecked(terms.accept(consent.name), false)
    await terms.confirm.click()

    await expect(terms.root).toBeHidden()
    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    expect(
      decisionsOf(await termsState(participant.api, event.id), participant.account.id),
    ).toStrictEqual({
      [conditions.id]: true,
      [consent.id]: false,
    })
  })

  test('a later signup asks again only for the consent that was declined', async ({
    seed,
    member,
    pageAs,
  }) => {
    const conditions = await seed.termsDocument({ kind: 'Conditions', requiresSignature: true })
    const consent = await seed.termsDocument({
      kind: 'Consent',
      name: uniqueTitle('Consentimiento'),
    })
    const event = await eventOnFreeDay(seed, {
      terms: [
        { document: conditions, required: true },
        { document: consent, required: false },
      ],
    })
    const first = await seed.activity(event, { startHour: 10 })
    const second = await seed.activity(event, { startHour: 15 })
    await signUp(member.api, first, member.account.id, 'participant', [consent.id])
    const page = await pageAs(member)
    const card = await new EventDetailPage(page).openActivity(event, second)
    const terms = new TermsDialog(page)

    await card.signUpAs('volunteer')

    await expect(terms.open(consent.name)).toBeVisible()
    await expectChecked(terms.accept(consent.name), false)
    await expect(terms.open(conditions.name)).toBeHidden()
    await expect(terms.accept(conditions.name)).toHaveCount(0)
    await setChecked(terms.accept(consent.name), true)
    await terms.confirm.click()
    await expect(terms.root).toBeHidden()
    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    expect(decisionsOf(await termsState(member.api, event.id), member.account.id)).toStrictEqual({
      [conditions.id]: true,
      [consent.id]: true,
    })
  })

  test('a person who accepted every document signs up again without the dialog', async ({
    seed,
    participant,
    pageAs,
  }) => {
    const conditions = await seed.termsDocument({ kind: 'Conditions' })
    const event = await eventOnFreeDay(seed, { terms: [{ document: conditions, required: true }] })
    const first = await seed.activity(event, { startHour: 10 })
    const second = await seed.activity(event, { startHour: 15 })
    await signUp(participant.api, first, participant.account.id)
    const page = await pageAs(participant)
    const card = await new EventDetailPage(page).openActivity(event, second)

    await card.signUpAs('participant')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(new TermsDialog(page).root).toBeHidden()
    await expect(card.status).toHaveText(STATUS_LABELS.requested)
    const signups = await assignedActivities(participant.api, event.id)
    expect(signups.map((signup) => signup.activityId).sort()).toStrictEqual(
      [first.id, second.id].sort(),
    )
  })
})
