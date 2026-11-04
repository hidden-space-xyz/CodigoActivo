import { signUp } from '../../support/api/signups'
import {
  assignedActivities,
  decisionsOf,
  eventOnFreeDay,
  signUpDeclining,
  termsState,
} from '../../support/api/signups-extra'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard } from '../../support/pages/signups/activity-card'
import { TermsDialog } from '../../support/pages/signups/terms-dialog'
import { notification, setChecked } from '../../support/ui/element-plus'

const SIGNUP_SENT = 'Hemos recibido tu solicitud. Te avisaremos por correo cuando se revise.'
const MISSING_REQUIRED = 'Debes marcar todos los documentos obligatorios para continuar.'
const MISSING_SIGNATURE = 'Firma los documentos marcados que lo requieren para continuar.'
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
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    const terms = new TermsDialog(page)

    await card.signUpAs('Participante')
    await expect(terms.root).toBeVisible()
    await expect(terms.accept(conditions.name)).not.toBeChecked()
    await expect(terms.accept(consent.name)).not.toBeChecked()
    await expect(terms.signature(conditions.name)).toBeHidden()
    await terms.confirm.click()
    await expect(terms.root.getByText(MISSING_REQUIRED)).toBeVisible()

    await setChecked(terms.accept(conditions.name), true)
    await expect(terms.root.getByText(MISSING_REQUIRED)).toBeHidden()
    await expect(terms.signature(conditions.name)).toBeVisible()
    await expect(terms.clearSignature(conditions.name)).toBeDisabled()
    await terms.confirm.click()
    await expect(terms.root.getByText(MISSING_SIGNATURE)).toBeVisible()

    await terms.sign(conditions.name)
    await expect(terms.clearSignature(conditions.name)).toBeEnabled()
    await expect(terms.root.getByText(MISSING_SIGNATURE)).toBeHidden()

    await terms.open(consent.name).click()
    const preview = terms.preview(consent.name)
    await expect(preview.getByText(CONSENT_TEXT)).toBeVisible()
    await preview.getByRole('button', { name: 'Aceptar' }).click()
    await expect(preview).toBeHidden()
    await expect(terms.accept(consent.name)).toBeChecked()
    await terms.open(consent.name).click()
    await preview.getByRole('button', { name: 'Rechazar' }).click()
    await expect(preview).toBeHidden()
    await expect(terms.accept(consent.name)).not.toBeChecked()
    await terms.confirm.click()

    await expect(terms.root).toBeHidden()
    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText('Solicitada')
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
    await signUpDeclining(member.api, first, member.account.id, 'participant', [consent.id])
    const page = await pageAs(member)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(second.title))
    const terms = new TermsDialog(page)

    await card.signUpAs('Voluntario')

    await expect(terms.open(consent.name)).toBeVisible()
    await expect(terms.accept(consent.name)).not.toBeChecked()
    await expect(terms.open(conditions.name)).toBeHidden()
    await expect(terms.accept(conditions.name)).toHaveCount(0)
    await setChecked(terms.accept(consent.name), true)
    await terms.confirm.click()
    await expect(terms.root).toBeHidden()
    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(card.status).toHaveText('Solicitada')
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
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(second.title))

    await card.signUpAs('Participante')

    await expect(notification(page, SIGNUP_SENT)).toBeVisible()
    await expect(new TermsDialog(page).root).toBeHidden()
    await expect(card.status).toHaveText('Solicitada')
    const signups = await assignedActivities(participant.api, event.id)
    expect(signups.map((signup) => signup.activityId).sort()).toStrictEqual(
      [first.id, second.id].sort(),
    )
  })
})
