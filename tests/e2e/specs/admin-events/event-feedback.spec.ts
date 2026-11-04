import { rateEvent, signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import {
  EventAdminPage,
  OpinionsTab,
  TermsTab,
} from '../../support/pages/admin-events/event-admin-page'
import { firstDependent } from '../../support/api/accounts'
import { fullName } from '../../support/data/people'
import { confirm, notification } from '../../support/ui/element-plus'

test.describe('event opinions and terms admin', () => {
  test('the opinions tab shows an attendee rating and the page its average', async ({
    pageAs,
    admin,
    seed,
    member,
  }) => {
    const signedUp = await seed.event()
    const activity = await seed.activity(signedUp)
    await seed.enrol(member.api, activity, member.account.id)
    const event = await seed.moveToPast(signedUp, [activity], 1)
    await rateEvent(member.api, event.id, {
      score: 4,
      mostLiked: 'Programar el robot con mi grupo.',
      suggestions: 'Más tiempo para el último reto.',
    })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await expect(eventPage.summaryValue('Media de 1 puntuación')).toHaveText(/^4\s*\/5$/)

    await eventPage.openTab('Opiniones')

    const opinions = new OpinionsTab(page)
    await expect(opinions.root.getByText('1 valoración', { exact: true })).toBeVisible()
    await expect(opinions.ratings).toHaveCount(1)
    const rating = opinions.ratings
    await expect(rating).toContainText('Valoración de un asistente')
    await expect(rating).toContainText('4/5')
    await expect(rating.getByRole('term')).toHaveText([
      '¿Qué fue lo que MÁS te gustó?',
      '¿Tienes alguna sugerencia para eventos FUTUROS?',
    ])
    await expect(rating.getByRole('definition')).toHaveText([
      'Programar el robot con mi grupo.',
      'Más tiempo para el último reto.',
    ])
  })

  test('an event nobody rated says so', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: -1 })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openTab('Opiniones')

    await expect(
      new OpinionsTab(page).root.getByText('Este evento todavía no tiene valoraciones.'),
    ).toBeVisible()
    await expect(eventPage.summaryValue('Sin puntuaciones')).toHaveText('—')
  })

  test('the terms tab lists who decided each document and shows the signatures', async ({
    pageAs,
    admin,
    seed,
    member,
    guardian,
  }) => {
    const conditions = await seed.termsDocument({ requiresSignature: true })
    const consent = await seed.termsDocument({ kind: 'Consent' })
    const event = await seed.event({
      terms: [
        { document: conditions, required: true },
        { document: consent, required: false },
      ],
    })
    const activity = await seed.activity(event)
    const minor = firstDependent(guardian.account)
    await signUp(member.api, activity, member.account.id)
    await signUp(guardian.api, activity, minor.id)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openTab('Términos')

    const terms = new TermsTab(page)
    const memberName = fullName(member.account)
    const minorName = fullName(minor)
    const own = terms.decision(memberName, conditions.name)
    await expect(own).toContainText('La propia persona')
    await expect(own).toContainText('Aceptado')
    await expect(terms.decision(memberName, consent.name)).toContainText('Sin firma')
    const minorConditions = terms.decision(minorName, conditions.name)
    await expect(minorConditions).toContainText(fullName(guardian.account))
    await expect(minorConditions).toContainText('Aceptado')

    await own.getByRole('button', { name: `Ver la firma de ${memberName}` }).click()

    const signature = page.getByRole('dialog', { name: `Firma de ${memberName}` })
    await expect(
      signature.getByRole('img', { name: `Firma de ${memberName} para ${conditions.name}` }),
    ).toBeVisible()
  })

  test('an administrator records the withdrawal of a consent', async ({
    pageAs,
    admin,
    seed,
    member,
  }) => {
    const consent = await seed.termsDocument({ kind: 'Consent' })
    const event = await seed.event({ terms: [{ document: consent, required: false }] })
    const activity = await seed.activity(event)
    await signUp(member.api, activity, member.account.id)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openTab('Términos')
    const terms = new TermsTab(page)
    const decision = terms.decision(fullName(member.account), consent.name)
    await expect(decision).toContainText('Aceptado')

    await decision.getByRole('button', { name: 'Registrar retirada', exact: true }).click()
    await confirm(page, 'Registrar la retirada de un consentimiento', 'Registrar retirada')

    await expect(notification(page, 'Retirada registrada.')).toBeVisible()
    await expect(decision).toContainText('Retirado')
    await expect(decision.getByRole('button', { name: 'Registrar retirada' })).toBeHidden()
  })
})
