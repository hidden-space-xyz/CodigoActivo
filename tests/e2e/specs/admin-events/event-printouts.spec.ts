import { signUp } from '../../support/api/signups'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { EventAdminPage } from '../../support/pages/admin-events/event-admin-page'
import { firstDependent } from '../../support/api/accounts'
import { fullName } from '../../support/data/people'
import { BadgesPage, RosterPage, stubPrint } from '../../support/pages/admin-events/printout-pages'

test.describe('event printouts', () => {
  test('the badges show one label per confirmed attendee and print', async ({
    pageAs,
    admin,
    seed,
    participant,
    guardian,
    member,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event, { location: uniqueTitle('Aula') })
    const minor = firstDependent(guardian.account)
    await seed.enrol(activity, participant.account.id)
    await seed.enrol(activity, minor.id)
    await signUp(member.api, activity, member.account.id)
    const page = await pageAs(admin)
    await stubPrint(page)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.printBadges.click()

    await expect(page).toHaveURL(`${EventAdminPage.path(event.id)}/badges`)
    const badges = new BadgesPage(page)
    await expect(badges.badges).toHaveCount(2)
    const adult = badges.badge(fullName(participant.account))
    await expect(adult).toContainText(event.title)
    await expect(adult).toContainText(activity.title)
    await expect(adult).toContainText(activity.location)
    await expect(adult).toContainText('Participante')
    const child = badges.badge(fullName(minor))
    await expect(child).toContainText(guardian.account.firstName)
    await expect(child).toContainText(guardian.account.phone)
    await expect(badges.badge(fullName(member.account))).toBeHidden()

    await badges.print.click()
    await badges.expectPrintCalls(1)

    await badges.back.click()
    await expect(page).toHaveURL(EventAdminPage.path(event.id))
  })

  test('the roster lists the confirmed attendees of each activity and prints', async ({
    pageAs,
    admin,
    seed,
    participant,
    guardian,
    member,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    const minor = firstDependent(guardian.account)
    await seed.enrol(activity, participant.account.id)
    await seed.enrol(activity, minor.id)
    await signUp(member.api, activity, member.account.id)
    const page = await pageAs(admin)
    await stubPrint(page)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.printRoster.click()

    await expect(page).toHaveURL(`${EventAdminPage.path(event.id)}/roster`)
    const roster = new RosterPage(page)
    await expect(roster.activity(activity.title)).toBeVisible()
    await expect(page.getByText('2 inscritos').filter({ visible: true })).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Participantes', exact: true })).toBeVisible()
    await expect(roster.person(fullName(participant.account))).toContainText(
      participant.account.email,
    )
    await expect(roster.person(fullName(minor))).toContainText(fullName(guardian.account))
    await expect(roster.person(fullName(member.account))).toBeHidden()

    await roster.print.click()
    await roster.expectPrintCalls(1)

    await roster.back.click()
    await expect(page).toHaveURL(EventAdminPage.path(event.id))
  })

  test('with nobody confirmed there is nothing to print', async ({
    pageAs,
    admin,
    seed,
    participant,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    const page = await pageAs(admin)

    await page.goto(`${EventAdminPage.path(event.id)}/badges`)

    const badges = new BadgesPage(page)
    await expect(badges.empty).toBeVisible()
    await expect(badges.badges).toHaveCount(0)

    await page.goto(`${EventAdminPage.path(event.id)}/roster`)

    const roster = new RosterPage(page)
    await expect(roster.empty).toBeVisible()
    await expect(roster.activity(activity.title)).toBeHidden()
  })
})
