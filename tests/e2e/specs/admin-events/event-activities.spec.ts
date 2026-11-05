import { CATALOG } from '../../support/api/catalogs'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import {
  ActivityFormDialog,
  EventAdminPage,
  MODALITY_LABELS,
  ROLE_LABELS,
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

    await expect(eventPage.subtitle).toHaveText(event.subtitle)
    await expect(eventPage.summaryValue(t('pages.admin.eventDetail.tabs.activities'))).toHaveText(
      '2',
    )
    await expect(eventPage.summaryValue(ROLE_LABELS.leader)).toHaveText('1')
    await expect(eventPage.summaryValue(ROLE_LABELS.participant)).toHaveText('0')
    await expect(eventPage.summaryValue(ROLE_LABELS.volunteer)).toHaveText('0')
    await expect(
      eventPage.summaryValue(t('pages.admin.eventDetail.summary.ratings', { count: 0 })),
    ).toHaveText('—')
    await expect(eventPage.activities.row(activity.title)).toBeVisible()
  })

  test('an administrator creates an activity with role targets and an image', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const title = uniqueTitle('Taller')
    const location = 'Aula Magna'
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.newActivity.click()
    const dialog = new ActivityFormDialog(page)
    await dialog.title.fill(title)
    await dialog.description.fill('Construimos un robot seguidor de líneas.')
    await dialog.chooseModality('onSite')
    await dialog.location.fill(location)
    await fillPicker(dialog.startsAt, pickerMoment(dayFromToday(EVENT_DAY), 10))
    await fillPicker(dialog.endsAt, pickerMoment(dayFromToday(EVENT_DAY), 12, 30))
    await dialog.capacity('leader').fill('2')
    await dialog.capacity('participant').fill('15')
    await dialog.capacity('volunteer').fill('4')
    await pickThumbnail(dialog.root)
    await dialog.save.click()

    await expect(
      notification(page, t('pages.admin.eventDetail.toast.activityCreated')),
    ).toBeVisible()
    await expect(dialog.root).toBeHidden()
    const row = eventPage.activities.row(title)
    await expect(row).toContainText(location)
    await expect(row).toContainText(MODALITY_LABELS.onSite)
    await expect(eventPage.summaryValue(t('pages.admin.eventDetail.tabs.activities'))).toHaveText(
      '1',
    )

    await eventPage.activities.rowAction(title, 'edit')
    const saved = new ActivityFormDialog(page)
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
    const dialog = new ActivityFormDialog(page)

    await dialog.save.click()

    for (const [error, message] of [
      ['activity-form-modality-error', 'modalityRequired'],
      ['activity-form-location-error', 'locationRequired'],
      ['activity-form-start-error', 'startRequired'],
      ['activity-form-end-error', 'endRequired'],
    ] as const) {
      await expect(dialog.root.getByTestId(error)).toHaveText(
        t(`pages.admin.eventDetail.activities.form.problems.${message}`),
      )
    }
    await expect(dialog.root.getByTestId('thumbnail-field')).toContainText(
      t('common.imageRequired'),
    )
    await expect(dialog.title).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.description).toHaveAttribute('aria-invalid', 'true')
    await expect(dialog.root).toBeVisible()
  })

  test('an administrator renames and moves an activity', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const activity = await seed.activity(event, { location: 'León' })
    const renamed = uniqueTitle('Taller renombrado')
    const moved = 'Biblioteca municipal'
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.activities.rowAction(activity.title, 'edit')
    const dialog = new ActivityFormDialog(page)
    await expect(dialog.title).toHaveValue(activity.title)
    await expect(dialog.capacity('participant')).toHaveValue('10')
    await dialog.title.fill(renamed)
    await dialog.location.fill(moved)
    await dialog.save.click()

    await expect(
      notification(page, t('pages.admin.eventDetail.toast.activityUpdated')),
    ).toBeVisible()
    await expect(dialog.root).toBeHidden()
    await expect(eventPage.activities.row(renamed)).toContainText(moved)
    await expect(eventPage.activities.row(activity.title)).toBeHidden()
  })

  test('an administrator deletes an activity after confirming', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const activity = await seed.activity(event)
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)

    await eventPage.activities.rowAction(activity.title, 'delete')
    await confirm(page)

    await expect(
      notification(page, t('pages.admin.eventDetail.toast.activityDeleted')),
    ).toBeVisible()
    await expect(eventPage.activities.row(activity.title)).toBeHidden()
    await expect(eventPage.activities.root).toContainText(t('pages.admin.eventDetail.empty.none'))
    await expect(eventPage.summaryValue(t('pages.admin.eventDetail.tabs.activities'))).toHaveText(
      '0',
    )
  })

  test('the modality filter keeps only the online activities', async ({ pageAs, admin, seed }) => {
    const event = await seed.event({ startsIn: EVENT_DAY })
    const online = await seed.activity(event, { online: true, location: 'Jitsi' })
    const onSite = await seed.activity(event, { startHour: 16 })
    const page = await pageAs(admin)
    const eventPage = new EventAdminPage(page)
    await eventPage.goto(event.id, event.title)
    await expect(eventPage.activities.row(onSite.title)).toBeVisible()

    await eventPage.activities.filterBy('event-detail-modality-filter', CATALOG.modalities.online)

    await expect(eventPage.activities.row(online.title)).toBeVisible()
    await expect(eventPage.activities.row(onSite.title)).toBeHidden()
    await expect(eventPage.activities.count).toHaveText(t('table.results', { n: 1 }))
  })
})
