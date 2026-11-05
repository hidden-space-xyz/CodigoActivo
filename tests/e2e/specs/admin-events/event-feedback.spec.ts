import { rateEvent, signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
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
    const answers = {
      score: 4,
      mostLiked: 'Programar el robot con mi grupo.',
      suggestions: 'Más tiempo para el último reto.',
    }
    await rateEvent(member.api, event.id, answers)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await expect(
      eventPage.summaryValue(t('pages.admin.eventDetail.summary.ratings', { count: 1 })),
    ).toHaveText(/^4\s*\/5$/)

    await eventPage.openTab('opinions')

    const opinions = new OpinionsTab(page)
    await expect(opinions.count).toHaveText(
      t('pages.admin.eventDetail.opinions.count', { count: 1 }),
    )
    await expect(opinions.ratings).toHaveCount(1)
    const rating = opinions.ratings
    await expect(rating).toContainText(t('pages.admin.eventDetail.opinions.author'))
    await expect(rating).toContainText('4/5')
    await expect(rating.getByTestId('event-opinion-question')).toHaveText([
      t('entities.event.ratingQuestions.mostLiked'),
      t('entities.event.ratingQuestions.suggestions'),
    ])
    await expect(rating.getByTestId('event-opinion-answer')).toHaveText([
      answers.mostLiked,
      answers.suggestions,
    ])
  })

  test('an event nobody rated says so', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: -1 })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.openTab('opinions')

    await expect(new OpinionsTab(page).empty).toHaveText(
      t('pages.admin.eventDetail.opinions.empty'),
    )
    await expect(
      eventPage.summaryValue(t('pages.admin.eventDetail.summary.ratings', { count: 0 })),
    ).toHaveText('—')
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

    await eventPage.openTab('terms')

    const terms = new TermsTab(page)
    const memberName = fullName(member.account)
    const minorName = fullName(minor)
    const own = terms.decision(memberName, conditions.name)
    await expect(own).toContainText(t('pages.admin.eventDetail.terms.decidedBySelf'))
    await expect(own).toContainText(t('pages.admin.eventDetail.terms.accepted'))
    await expect(terms.decision(memberName, consent.name)).toContainText(
      t('pages.admin.eventDetail.terms.noSignature'),
    )
    const minorConditions = terms.decision(minorName, conditions.name)
    await expect(minorConditions).toContainText(fullName(guardian.account))
    await expect(minorConditions).toContainText(t('pages.admin.eventDetail.terms.accepted'))

    await own.getByTestId('event-terms-signature-open').click()

    const signature = page
      .getByTestId('event-terms-signature-dialog')
      .getByTestId('event-terms-signature-image')
    await expect(signature).toBeVisible()
    await expect(signature).toHaveAccessibleName(
      t('pages.admin.eventDetail.terms.signatureAlt', {
        name: memberName,
        document: conditions.name,
      }),
    )
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
    await eventPage.openTab('terms')
    const terms = new TermsTab(page)
    const decision = terms.decision(fullName(member.account), consent.name)
    await expect(decision).toContainText(t('pages.admin.eventDetail.terms.accepted'))

    await decision.getByTestId('row-action-withdraw').click()
    await confirm(page)

    await expect(notification(page, t('pages.admin.eventDetail.terms.withdraw.done'))).toBeVisible()
    await expect(decision).toContainText(t('pages.admin.eventDetail.terms.withdrawn'))
    await expect(decision.getByTestId('row-action-withdraw')).toBeHidden()
  })
})
