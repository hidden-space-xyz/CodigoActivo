import { eventById, eventTitled, findEvent } from '../../support/api/admin-events'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { EventAdminPage } from '../../support/pages/admin-events/event-admin-page'
import {
  EventFormDialog,
  EventsAdminPage,
} from '../../support/pages/admin-events/events-admin-page'
import { pickerDate, pickerMoment } from '../../support/data/formats'
import { dayFromToday } from '../../support/data/time'
import { confirm, expectChecked, notification } from '../../support/ui/element-plus'

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
    const dialog = new EventFormDialog(page)
    await dialog.title.fill(title)
    await dialog.subtitle.fill('Jornada abierta de programación creativa')
    await dialog.chooseCategory(category.id)
    await dialog.createCategory(newCategory)
    await dialog.chooseTerms(conditions.id)
    await expectChecked(dialog.termsRequired(conditions.name), true)
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

    await expect(notification(page, t('pages.admin.events.toasts.created'))).toBeVisible()
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
    const dialog = new EventFormDialog(page)

    await dialog.save.click()

    for (const [error, message] of [
      ['event-form-categories-error', 'categoriesRequired'],
      ['event-form-starts-on-error', 'eventStartRequired'],
      ['event-form-ends-on-error', 'eventEndRequired'],
      ['event-form-signup-from-error', 'signupStartRequired'],
      ['event-form-signup-until-error', 'signupEndRequired'],
    ] as const) {
      await expect(dialog.root.getByTestId(error)).toHaveText(
        t(`pages.admin.events.form.problems.${message}`),
      )
    }
    await expect(dialog.root.getByTestId('thumbnail-field')).toContainText(
      t('common.imageRequired'),
    )
    await expect(dialog.title).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.subtitle).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.root).toBeVisible()
  })

  test('signup moments out of order are refused before saving', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.newEvent.click()
    const dialog = new EventFormDialog(page)
    await dialog.schedule({
      startsOn: pickerDate(dayFromToday(20)),
      endsOn: pickerDate(dayFromToday(20)),
      signupFrom: pickerMoment(dayFromToday(5), 18),
      signupUntil: pickerMoment(dayFromToday(5), 9),
      earlySignupFrom: pickerMoment(dayFromToday(5), 19),
    })

    await dialog.save.click()

    await expect(dialog.root.getByTestId('event-form-signup-until-error')).toHaveText(
      t('pages.admin.events.form.problems.signupOrderInvalid'),
    )
    await expect(dialog.root.getByTestId('event-form-early-signup-from-error')).toHaveText(
      t('pages.admin.events.form.problems.earlySignupOrderInvalid'),
    )
    await expect(dialog.root.getByTestId('event-form-starts-on-error')).toBeHidden()
  })

  test('an administrator renames an event', async ({ pageAs, admin, seed }) => {
    const event = await seed.event()
    const renamed = uniqueTitle('Evento renombrado')
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(event.title)

    await events.table.rowAction(event.title, 'edit')
    const dialog = new EventFormDialog(page)
    await expect(dialog.title).toHaveValue(event.title)
    await expect(dialog.subtitle).toHaveValue(event.subtitle)
    await dialog.title.fill(renamed)
    await dialog.save.click()

    await expect(notification(page, t('pages.admin.events.toasts.updated'))).toBeVisible()
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

    await events.table.rowAction(event.title, 'delete')
    await confirm(page)

    await expect(notification(page, t('pages.admin.events.toasts.deleted'))).toBeVisible()
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

    await events.showScope('past')
    await expect(events.table.row(past.title)).toBeVisible()
    await expect(events.table.row(upcoming.title)).toBeHidden()

    await events.showScope('all')
    await expect(events.table.row(past.title)).toBeVisible()
    await expect(events.table.row(upcoming.title)).toBeVisible()
    await expect(events.table.count).toHaveText(t('table.results', { n: 2 }))
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
    await expect(events.table.count).toHaveText(t('table.results', { n: 1 }))
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

    await events.table.filterBy('admin-events-category-filter', robotics.id)

    await expect(events.table.row(wanted.title)).toBeVisible()
    await expect(events.table.row(other.title)).toBeHidden()
    await expect(
      events.table.chip(
        t('table.filterChip', {
          label: t('pages.admin.events.columns.categories'),
          value: robotics.name,
        }),
      ),
    ).toBeVisible()
  })

  test('managing an event opens its admin page', async ({ pageAs, admin, seed }) => {
    const event = await seed.event()
    const page = await pageAs(admin)
    const events = new EventsAdminPage(page)
    await events.open()
    await events.find(event.title)

    await events.table.rowAction(event.title, 'manage')

    await expect(page).toHaveURL(EventAdminPage.path(event.id))
    await expect(new EventAdminPage(page).heading(event.title)).toBeVisible()
  })
})
