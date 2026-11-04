import { historyEntry } from '../../support/api/account'
import { signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { HistoryTab, RatingDialog } from '../../support/pages/account/history-tab'
import { confirm, notification } from '../../support/ui/element-plus'

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
    await seed.enrol(confirmed, actor.account.id)
    const attended = await seed.event({ startsIn: -10 })
    await seed.enrol(await seed.activity(attended), actor.account.id)
    const missed = await seed.event({ startsIn: -12 })
    await seed.enrol(await seed.activity(missed), actor.account.id, 'participant', 'denied')

    const page = await pageAs(actor)
    await new AccountPage(page).goto('Historial')
    const history = new HistoryTab(page)

    const next = history.event('Próximos eventos', upcoming.title)
    await expect(next).toContainText('2 actividades')
    await expect(next.getByRole('button', { name: 'Valorar', exact: true })).toBeHidden()
    await history.toggle(next).click()
    await expect(history.toggle(next)).toHaveAttribute('aria-expanded', 'true')
    await expect(history.activity(next, requested.title)).toContainText('Solicitada')
    await expect(history.activity(next, confirmed.title)).toContainText('Confirmada')

    const previous = history.event('Eventos anteriores', attended.title)
    await expect(previous).toContainText('1 actividad')
    await expect(previous.getByRole('button', { name: 'Valorar', exact: true })).toBeVisible()
    await expect(page.getByText(missed.title)).toBeHidden()
    expect(await historyEntry(actor.api, missed.id)).toBeUndefined()
  })

  test('a past event is rated, the rating edited and then deleted', async ({
    accounts,
    pageAs,
    seed,
  }) => {
    const actor = await accounts.actor({ label: 'rating' })
    const event = await seed.event({ startsIn: -10 })
    await seed.enrol(await seed.activity(event), actor.account.id)
    const page = await pageAs(actor)
    await new AccountPage(page).goto('Historial')
    const entry = new HistoryTab(page).event('Eventos anteriores', event.title)
    const rating = new RatingDialog(page)

    await entry.getByRole('button', { name: 'Valorar', exact: true }).click()
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

    await expect(notification(page, 'Valoración guardada')).toBeVisible()
    await expect(rating.root).toBeHidden()
    const edit = entry.getByRole('button', { name: 'Editar valoración' })
    await expect(edit).toBeVisible()
    expect((await historyEntry(actor.api, event.id))?.rating).toMatchObject({
      score: 4,
      mostLiked: 'Los retos por parejas',
    })

    await edit.click()
    await expect(rating.mostLiked).toHaveValue('Los retos por parejas')
    await rating.rate(5)
    await rating.suggestions.fill('Más talleres de robótica')
    await rating.saveChanges.click()
    await expect(notification(page, 'Valoración guardada')).toBeVisible()
    await expect(rating.root).toBeHidden()
    expect((await historyEntry(actor.api, event.id))?.rating).toMatchObject({
      score: 5,
      mostLiked: 'Los retos por parejas',
      suggestions: 'Más talleres de robótica',
    })

    await edit.click()
    await rating.remove.click()
    await confirm(page, 'Borrar valoración', 'Eliminar')
    await expect(notification(page, 'Tu valoración se ha borrado.')).toBeVisible()
    await expect(rating.root).toBeHidden()
    await expect(entry.getByRole('button', { name: 'Valorar', exact: true })).toBeVisible()
    expect((await historyEntry(actor.api, event.id))?.rating).toBeNull()
  })
})
