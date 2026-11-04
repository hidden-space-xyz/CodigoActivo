import { eventById, eventTitled, findEvent } from '../../support/api/admin-events'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { EventAdminPage } from '../../support/pages/admin-events/event-admin-page'
import {
  EventFormDialog,
  EventsAdminPage,
} from '../../support/pages/admin-events/events-admin-page'
import { pickerDate, pickerMoment } from '../../support/data/formats'
import { dayFromToday } from '../../support/data/time'
import { confirm, notification } from '../../support/ui/element-plus'

test.describe('events admin', () => {
  test('an administrator creates an event with categories, optional terms and an image', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const category = await seed.category()
    const conditions = await seed.termsDocument()
    const newCategory = uniqueTitle('Categoría')
    const title = uniqueTitle('Evento')
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()

    await events.newEvent.click()
    const dialog = new EventFormDialog(page, 'Nuevo evento')
    await dialog.title.fill(title)
    await dialog.subtitle.fill('Jornada abierta de programación creativa')
    await dialog.chooseCategory(category.name)
    await dialog.createCategory(newCategory)
    await dialog.chooseTerms(conditions.name)
    await expect(dialog.termsRequired(conditions.name)).toBeChecked()
    await dialog.setTermsRequired(conditions.name, false)
    await dialog.describe('Talleres de robótica y videojuegos para todas las edades.')
    await dialog.schedule({
      startsOn: pickerDate(dayFromToday(20)),
      endsOn: pickerDate(dayFromToday(21)),
      signupFrom: pickerMoment(dayFromToday(-1), 9),
      signupUntil: pickerMoment(dayFromToday(19), 20),
      earlySignupFrom: pickerMoment(dayFromToday(-3), 9),
    })
    await dialog.pickImage()
    await dialog.save.click()

    await expect(notification(page, 'Evento creado.')).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await events.find(title)
    const row = events.table.row(title)
    await expect(row).toContainText(category.name)
    await expect(row).toContainText(newCategory)
    const saved = await eventTitled(admin.api, title)
    expect(saved.subtitle).toBe('Jornada abierta de programación creativa')
    expect(saved.earlySignupStartsAt).toBeTruthy()
    expect(saved.categories.map((item) => item.name).sort()).toStrictEqual(
      [category.name, newCategory].sort(),
    )
    expect(saved.termsDocuments).toStrictEqual([
      expect.objectContaining({ termsDocumentId: conditions.id, required: false }),
    ])
  })

  test('saving an empty event form shows what is missing', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.newEvent.click()
    const dialog = new EventFormDialog(page, 'Nuevo evento')

    await dialog.save.click()

    for (const message of [
      'Selecciona al menos una categoría.',
      'La fecha de inicio es obligatoria.',
      'La fecha de fin es obligatoria.',
      'La apertura de inscripción es obligatoria.',
      'El cierre de inscripción es obligatorio.',
      'La imagen es obligatoria.',
    ]) {
      await expect(dialog.root.getByText(message, { exact: true })).toBeVisible()
    }
    await expect(dialog.title).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.subtitle).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.root).toBeVisible()
  })

  test('signup moments out of order are refused before saving', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.newEvent.click()
    const dialog = new EventFormDialog(page, 'Nuevo evento')
    await dialog.schedule({
      startsOn: pickerDate(dayFromToday(20)),
      endsOn: pickerDate(dayFromToday(20)),
      signupFrom: pickerMoment(dayFromToday(5), 18),
      signupUntil: pickerMoment(dayFromToday(5), 9),
      earlySignupFrom: pickerMoment(dayFromToday(5), 19),
    })

    await dialog.save.click()

    await expect(
      dialog.root.getByText('El cierre debe ser posterior a la apertura.', { exact: true }),
    ).toBeVisible()
    await expect(
      dialog.root.getByText(
        'La inscripción anticipada debe abrir antes que la inscripción general.',
        { exact: true },
      ),
    ).toBeVisible()
    await expect(dialog.root.getByText('La fecha de inicio es obligatoria.')).toBeHidden()
  })

  test('an administrator renames an event', async ({ pageAs, admin, seed }) => {
    const event = await seed.event()
    const renamed = uniqueTitle('Evento renombrado')
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(event.title)

    await events.table.rowAction(event.title, 'Editar')
    const dialog = new EventFormDialog(page, 'Editar evento')
    await expect(dialog.title).toHaveValue(event.title)
    await expect(dialog.subtitle).toHaveValue(event.subtitle)
    await dialog.title.fill(renamed)
    await dialog.save.click()

    await expect(notification(page, 'Evento actualizado.')).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await events.find(renamed)
    await expect(events.table.row(renamed)).toBeVisible()
    expect((await eventById(admin.api, event.id)).title).toBe(renamed)
  })

  test('an administrator deletes an event after confirming', async ({ pageAs, admin, seed }) => {
    const event = await seed.event()
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(event.title)

    await events.table.rowAction(event.title, 'Eliminar', `Más acciones para ${event.title}`)
    await confirm(page, 'Eliminar evento', 'Eliminar')

    await expect(notification(page, 'Evento eliminado.')).toBeVisible()
    await expect(events.table.row(event.title)).toBeHidden()
    expect(await findEvent(admin.api, event.title)).toBeUndefined()
  })

  test('the scope switch shows upcoming, past or all events', async ({ pageAs, admin, seed }) => {
    const token = uniqueLetters()
    const upcoming = await seed.event({ title: `Próximo e2e ${token}` })
    const past = await seed.event({ title: `Pasado e2e ${token}`, startsIn: -1 })
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(token)

    await expect(events.table.row(upcoming.title)).toBeVisible()
    await expect(events.table.row(past.title)).toBeHidden()

    await events.showScope('Pasados')
    await expect(events.table.row(past.title)).toBeVisible()
    await expect(events.table.row(upcoming.title)).toBeHidden()

    await events.showScope('Todos')
    await expect(events.table.row(past.title)).toBeVisible()
    await expect(events.table.row(upcoming.title)).toBeVisible()
    await expect(events.table.count).toHaveText('2 resultados')
  })

  test('the search finds an event by its subtitle', async ({ pageAs, admin, seed }) => {
    const token = uniqueLetters()
    const wanted = await seed.event({ subtitle: `Jornada de puertas abiertas ${token}` })
    const other = await seed.event()
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()

    await events.find(token)

    await expect(events.table.row(wanted.title)).toBeVisible()
    await expect(events.table.row(other.title)).toBeHidden()
    await expect(events.table.count).toHaveText('1 resultado')
  })

  test('the category filter keeps only the events of that category', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const token = uniqueLetters()
    const robotics = await seed.category()
    const music = await seed.category()
    const wanted = await seed.event({ title: `Robótica e2e ${token}`, categories: [robotics] })
    const other = await seed.event({ title: `Música e2e ${token}`, categories: [music] })
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(token)
    await expect(events.table.row(other.title)).toBeVisible()

    await events.table.filterBy('Categorías', robotics.name)

    await expect(events.table.row(wanted.title)).toBeVisible()
    await expect(events.table.row(other.title)).toBeHidden()
    await expect(events.table.chips.getByText(`Categorías: ${robotics.name}`)).toBeVisible()
  })

  test('managing an event opens its admin page', async ({ pageAs, admin, seed }) => {
    const event = await seed.event()
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(event.title)

    await events.table.rowAction(event.title, 'Gestionar')

    await expect(page).toHaveURL(EventAdminPage.path(event.id))
    await expect(new EventAdminPage(page).heading(event.title)).toBeVisible()
  })
})
