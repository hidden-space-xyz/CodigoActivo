import type { EventResponse } from '@contract/eventResponse'

import { STATUS_LABELS, type SignupStatusName } from '../../support/api/catalogs'
import type { ContentSeeder } from '../../support/api/content'
import { historyEntry } from '../../support/api/my-account'
import { signUp } from '../../support/api/signups'
import type { Actor } from '../../support/fixtures/actors'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AccountPage } from '../../support/pages/account/account-page'
import { HistoryTab, RatingDialog } from '../../support/pages/account/history-tab'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

/** An event that started `daysAgo` days ago, with one activity `actor` signed up for. */
async function pastEventWith(
  seed: ContentSeeder,
  actor: Actor,
  daysAgo: number,
  status: SignupStatusName,
): Promise<EventResponse> {
  const event = await seed.event()
  const activity = await seed.activity(event)
  await seed.enrol(actor.api, activity, actor.account.id, 'participant', status)
  return seed.moveToPast(event, [activity], daysAgo)
}

test.describe('event history', () => {
  test('upcoming signups show their status and past events only the confirmed ones', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'history' })
    const upcoming = await seed.event()
    const requested = await seed.activity(upcoming)
    const confirmed = await seed.activity(upcoming, { startHour: 16 })
    await signUp(actor.api, requested, actor.account.id)
    await seed.enrol(actor.api, confirmed, actor.account.id)
    const attended = await pastEventWith(seed, actor, 10, 'confirmed')
    const missed = await pastEventWith(seed, actor, 12, 'denied')

    const page = await pageAs(actor)
    await new AccountPage(page).goto('history')
    const history = new HistoryTab(page)

    const next = history.event('upcoming', upcoming.title)
    await expect(next).toContainText(t('pages.account.history.activityCount', { count: 2 }))
    await expect(next.getByTestId('history-event-rate')).toBeHidden()
    await history.toggle(next).click()
    await expect(history.toggle(next)).toHaveAttribute('aria-expanded', 'true')
    await expect(history.activity(next, requested.title)).toContainText(STATUS_LABELS.requested)
    await expect(history.activity(next, confirmed.title)).toContainText(STATUS_LABELS.confirmed)

    const previous = history.event('past', attended.title)
    await expect(previous).toContainText(t('pages.account.history.activityCount', { count: 1 }))
    await expect(previous.getByTestId('history-event-rate')).toHaveText(
      t('pages.account.history.rate'),
    )
    await expect(
      page.getByTestId('history-event-title').filter({ hasText: missed.title }),
    ).toBeHidden()
    expect(await historyEntry(actor.api, missed.id)).toBeUndefined()
  })

  test('a past event is rated, the rating edited and then deleted', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'rating' })
    const event = await pastEventWith(seed, actor, 10, 'confirmed')
    const page = await pageAs(actor)
    await new AccountPage(page).goto('history')
    const entry = new HistoryTab(page).event('past', event.title)
    const rating = new RatingDialog(page)
    const rate = entry.getByTestId('history-event-rate')

    await expect(rate).toHaveText(t('pages.account.history.rate'))
    await rate.click()
    await expect(rating.root).toContainText(event.title)
    await expect(rating.submit).toBeDisabled()
    await rating.mostLiked.fill('Los retos por parejas')
    await expect(rating.submit).toBeEnabled()
    await rating.mostLiked.fill('')
    await expect(rating.submit).toBeDisabled()
    await rating.rate(4)
    await expect(rating.score).toHaveAttribute('aria-valuenow', '4')
    await expect(rating.submit).toBeEnabled()
    await rating.mostLiked.fill('Los retos por parejas')
    await rating.submit.click()

    await expect(notification(page, t('pages.account.history.savedSummary'))).toBeVisible()
    await expect(rating.root).toBeHidden()
    await expect(rate).toHaveText(t('pages.account.history.editRating'))
    expect((await historyEntry(actor.api, event.id))?.rating).toMatchObject({
      score: 4,
      mostLiked: 'Los retos por parejas',
    })

    await rate.click()
    await expect(rating.mostLiked).toHaveValue('Los retos por parejas')
    await rating.rate(5)
    await rating.suggestions.fill('Más talleres de robótica')
    await rating.saveChanges.click()
    await expect(notification(page, t('pages.account.history.savedSummary'))).toBeVisible()
    await expect(rating.root).toBeHidden()
    expect((await historyEntry(actor.api, event.id))?.rating).toMatchObject({
      score: 5,
      mostLiked: 'Los retos por parejas',
      suggestions: 'Más talleres de robótica',
    })

    await rate.click()
    await rating.remove.click()
    await expect(messageBox(page)).toContainText(
      t('pages.account.history.dialog.deleteMessage', { event: event.title }),
    )
    await confirm(page)
    await expect(notification(page, t('pages.account.history.deletedDetail'))).toBeVisible()
    await expect(rating.root).toBeHidden()
    await expect(rate).toHaveText(t('pages.account.history.rate'))
    expect((await historyEntry(actor.api, event.id))?.rating).toBeNull()
  })
})
