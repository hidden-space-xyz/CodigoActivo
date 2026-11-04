import { addMinor, eventOnFreeDay, householdSignups } from '../../support/api/signups-extra'
import { expect, test } from '../../support/fixtures/test'
import { EventDetailPage } from '../../support/pages/event-detail-page'
import { ActivityCard } from '../../support/pages/signups/activity-card'
import { HouseholdDialog } from '../../support/pages/signups/household-dialog'
import { confirm, dialog, notification, setChecked } from '../../support/ui/element-plus'

test.describe('household signup', () => {
  test('a guardian signs the whole family up with a role for each person', async ({
    seed,
    accounts,
    pageAs,
  }) => {
    const guardian = await accounts.actor({ label: 'household' })
    const alba = await addMinor(guardian, 'Alba', 9)
    const bruno = await addMinor(guardian, 'Bruno', 12)
    const self = `${guardian.account.firstName} ${guardian.account.lastName}`
    const albaName = `${alba.firstName} ${alba.lastName}`
    const brunoName = `${bruno.firstName} ${bruno.lastName}`
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    const household = new HouseholdDialog(page)

    await expect(card.signUp).toBeHidden()
    await card.signUpFamily.click()
    await expect(household.root).toContainText(activity.title)
    await expect(household.person(guardian.account.firstName)).toBeChecked()
    await expect(household.person(albaName)).toBeChecked()
    await expect(household.person(brunoName)).toBeChecked()
    await household.expectRoles(albaName, ['Participante', 'Voluntario'])
    await household.chooseRole(guardian.account.firstName, 'Voluntario')
    await household.submit.click()
    await expect(
      notification(page, 'Selecciona un rol para cada integrante que quieras apuntar.'),
    ).toContainText('Falta el rol')
    await expect(household.root).toBeVisible()

    await household.chooseRole(albaName, 'Participante')
    await household.chooseRole(brunoName, 'Participante')
    await household.submit.click()

    await expect(household.root).toBeHidden()
    await expect(
      notification(
        page,
        'Hemos recibido las 3 solicitudes. Te avisaremos por correo cuando se revisen.',
      ),
    ).toContainText('Inscripción enviada')
    await expect(card.member(self, 'Voluntario')).toContainText('Solicitada')
    await expect(card.member(albaName, 'Participante')).toContainText('Solicitada')
    await expect(card.member(brunoName, 'Participante')).toContainText('Solicitada')
    await expect(
      card.root.getByText('Toda tu familia ya está inscrita en esta actividad.'),
    ).toBeVisible()
    await expect(card.signUpAnother).toBeHidden()
    const signups = await householdSignups(guardian.api, event.id)
    expect(signups.map((signup) => [signup.userId, signup.roleName]).sort()).toStrictEqual(
      [
        [guardian.account.id, 'Voluntario'],
        [alba.id, 'Participante'],
        [bruno.id, 'Participante'],
      ].sort(),
    )
  })

  test('a guardian signs part of the family up and withdraws one of them', async ({
    seed,
    accounts,
    pageAs,
  }) => {
    const guardian = await accounts.actor({ label: 'household' })
    const alba = await addMinor(guardian, 'Alba', 9)
    const bruno = await addMinor(guardian, 'Bruno', 12)
    const albaName = `${alba.firstName} ${alba.lastName}`
    const brunoName = `${bruno.firstName} ${bruno.lastName}`
    const event = await eventOnFreeDay(seed)
    const activity = await seed.activity(event)
    const page = await pageAs(guardian)
    const eventPage = new EventDetailPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.openActivities()
    const card = new ActivityCard(eventPage.activity(activity.title))
    const household = new HouseholdDialog(page)

    await card.signUpFamily.click()
    await setChecked(household.person(guardian.account.firstName), false)
    await household.chooseRole(albaName, 'Participante')
    await household.chooseRole(brunoName, 'Voluntario')
    await household.submit.click()

    await expect(
      notification(
        page,
        'Hemos recibido las 2 solicitudes. Te avisaremos por correo cuando se revisen.',
      ),
    ).toBeVisible()
    await expect(card.member(albaName, 'Participante')).toBeVisible()
    await expect(card.member(brunoName, 'Voluntario')).toBeVisible()
    await expect(card.signUpAnother).toBeVisible()

    await card.withdrawMember(albaName, 'Participante').click()
    await expect(dialog(page, 'Anular inscripción')).toContainText(
      `¿Quieres anular la inscripción de ${albaName} en «${activity.title}»?`,
    )
    await confirm(page, 'Anular inscripción', 'Anular inscripción')

    await expect(notification(page, 'Se ha eliminado la inscripción.')).toContainText(
      'Inscripción cancelada',
    )
    await expect(card.member(albaName, 'Participante')).toBeHidden()
    await expect(card.member(brunoName, 'Voluntario')).toBeVisible()
    expect(await householdSignups(guardian.api, event.id)).toMatchObject([
      { userId: bruno.id, roleName: 'Voluntario' },
    ])

    await card.signUpAnother.click()
    await expect(household.row(brunoName)).toContainText('Ya tiene inscripción como Voluntario')
    await expect(household.person(brunoName)).toHaveCount(0)
    await expect(household.person(albaName)).toBeChecked()
    await expect(household.person(guardian.account.firstName)).toBeChecked()
    await setChecked(household.person(guardian.account.firstName), false)
    await household.chooseRole(albaName, 'Voluntario')
    await household.submit.click()

    await expect(
      notification(page, 'Hemos recibido la solicitud. Te avisaremos por correo cuando se revise.'),
    ).toBeVisible()
    await expect(card.member(albaName, 'Voluntario')).toBeVisible()
    await expect(card.signUpAnother).toBeVisible()
  })
})
