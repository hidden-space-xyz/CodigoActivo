import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import {
  ActivityFormDialog,
  EventAdminPage,
} from '../../support/pages/admin-events/event-admin-page'
import { pickerMoment } from '../../support/data/formats'
import { dayFromToday } from '../../support/data/time'
import { confirm, fillPicker, notification } from '../../support/ui/element-plus'
import { pickThumbnail } from '../../support/ui/forms'

const EVENT_DAY = 14

test.describe('event activities admin', () => {
  test('the event page shows its title and the confirmed people per role', async ({
    pageAs,
    admin,
    seed,
    member,
    participant,
  }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const activity = await seed.activity(event)
    await seed.activity(event, { startHour: 16 })
    await seed.enrol(member.api, activity, member.account.id, 'leader')
    await seed.enrol(participant.api, activity, participant.account.id, 'participant', 'requested')
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)

    await eventPage.goto(event.id, event.title)

    await expect(eventPage.main.getByText(event.subtitle, { exact: true })).toBeVisible()
    await expect(eventPage.summaryValue('Actividades')).toHaveText('2')
    await expect(eventPage.summaryValue('Líder')).toHaveText('1')
    await expect(eventPage.summaryValue('Participante')).toHaveText('0')
    await expect(eventPage.summaryValue('Voluntario')).toHaveText('0')
    await expect(eventPage.summaryValue('Sin puntuaciones')).toHaveText('—')
    await expect(eventPage.activities.row(activity.title)).toBeVisible()
  })

  test('an administrator creates an activity with role targets and an image', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const title = uniqueTitle('Taller')
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.newActivity.click()
    const dialog = new ActivityFormDialog(page, 'Nueva actividad')
    await dialog.title.fill(title)
    await dialog.description.fill('Construimos un robot seguidor de líneas.')
    await dialog.chooseModality('Presencial')
    await dialog.location.fill('Aula Magna')
    await fillPicker(dialog.startsAt, pickerMoment(dayFromToday(EVENT_DAY), 10))
    await fillPicker(dialog.endsAt, pickerMoment(dayFromToday(EVENT_DAY), 12, 30))
    await dialog.capacity('leader').fill('2')
    await dialog.capacity('participant').fill('15')
    await dialog.capacity('volunteer').fill('4')
    await pickThumbnail(dialog.root)
    await dialog.save.click()

    await expect(notification(page, 'Actividad creada.')).toBeVisible()
    await expect(dialog.root).toBeHidden()
    const row = eventPage.activities.row(title)
    await expect(row).toContainText('Aula Magna')
    await expect(row).toContainText('Presencial')
    await expect(eventPage.summaryValue('Actividades')).toHaveText('1')

    await eventPage.activities.rowAction(title, 'Editar')
    const saved = new ActivityFormDialog(page, 'Editar actividad')
    await expect(saved.capacity('leader')).toHaveValue('2')
    await expect(saved.capacity('participant')).toHaveValue('15')
    await expect(saved.capacity('volunteer')).toHaveValue('4')
  })

  test('saving an empty activity form shows what is missing', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await eventPage.newActivity.click()
    const dialog = new ActivityFormDialog(page, 'Nueva actividad')

    await dialog.save.click()

    for (const message of [
      'La modalidad es obligatoria.',
      'La ubicación es obligatoria.',
      'La fecha y hora de inicio son obligatorias.',
      'La fecha y hora de fin son obligatorias.',
      'La imagen es obligatoria.',
    ]) {
      await expect(dialog.root.getByText(message, { exact: true })).toBeVisible()
    }
    await expect(dialog.title).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.description).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.root).toBeVisible()
  })

  test('an administrator renames and moves an activity', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const activity = await seed.activity(event, { location: 'León' })
    const renamed = uniqueTitle('Taller renombrado')
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.activities.rowAction(activity.title, 'Editar')
    const dialog = new ActivityFormDialog(page, 'Editar actividad')
    await expect(dialog.title).toHaveValue(activity.title)
    await expect(dialog.capacity('participant')).toHaveValue('10')
    await dialog.title.fill(renamed)
    await dialog.location.fill('Biblioteca municipal')
    await dialog.save.click()

    await expect(notification(page, 'Actividad actualizada.')).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await expect(eventPage.activities.row(renamed)).toContainText('Biblioteca municipal')
    await expect(eventPage.activities.row(activity.title)).toBeHidden()
  })

  test('an administrator deletes an activity after confirming', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const activity = await seed.activity(event)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.activities.rowAction(activity.title, 'Eliminar')
    await confirm(page, 'Eliminar actividad', 'Eliminar')

    await expect(notification(page, 'Actividad eliminada.')).toBeVisible()
    await expect(eventPage.activities.row(activity.title)).toBeHidden()
    await expect(
      eventPage.panel('Actividades').getByText('Este evento aún no tiene actividades.'),
    ).toBeVisible()
    await expect(eventPage.summaryValue('Actividades')).toHaveText('0')
  })

  test('the modality filter keeps only the online activities', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const online = await seed.activity(event, { online: true, location: 'Jitsi' })
    const onSite = await seed.activity(event, { startHour: 16 })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await expect(eventPage.activities.row(onSite.title)).toBeVisible()

    await eventPage.activities.filterBy('Modalidad', 'Online')

    await expect(eventPage.activities.row(online.title)).toBeVisible()
    await expect(eventPage.activities.row(onSite.title)).toBeHidden()
    await expect(eventPage.activities.count).toHaveText('1 resultado')
  })
})
