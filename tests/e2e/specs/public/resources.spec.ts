import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { CardListPage } from '../../support/pages/public/card-list-page'
import { cardDate, dayText } from '../../support/data/formats'

function externalUrl(): string {
  return `https://recursos.e2e.codigoactivo.test/guia-${uniqueLetters()}`
}

test.describe('resources', () => {
  test('an internal resource card opens its page', async ({ page, seed }) => {
    const resource = await seed.resource()
    const resources = new CardListPage(page, '/resources')
    await resources.goto()
    await resources.searchFor(resource.title)

    const card = resources.card(resource.title)
    await expect(card.getByRole('time')).toHaveText(
      cardDate('Publicado', dayText(resource.createdAt)),
    )
    await expect(card).toContainText(resource.subtitle)
    await expect(card).toHaveAttribute('href', `/resources/${resource.id}`)
    await card.click()

    await expect(page).toHaveURL(`/resources/${resource.id}`)
    const article = page.getByRole('main').getByRole('article')
    await expect(article.getByRole('heading', { level: 1, name: resource.title })).toBeVisible()
    await expect(article.getByText(resource.subtitle, { exact: true })).toBeVisible()
    await expect(article.getByRole('img', { name: resource.title })).toBeVisible()
    await expect(article.getByText('Guía paso a paso para el primer programa.')).toBeVisible()
    await page.getByRole('main').getByRole('link', { name: 'Volver a recursos' }).click()
    await expect(page).toHaveURL('/resources')
  })

  test('an external resource card links straight to its address', async ({ page, seed }) => {
    const url = externalUrl()
    const resource = await seed.resource({ url })
    const resources = new CardListPage(page, '/resources')
    await resources.goto()

    await resources.searchFor(resource.title)

    await expect(resources.card(resource.title)).toHaveAttribute('href', url)
  })

  test('the page of an external resource sends the visitor to its address', async ({
    page,
    seed,
  }) => {
    const url = externalUrl()
    const resource = await seed.resource({ url })
    await page.route(url, (route) =>
      route.fulfill({ contentType: 'text/html; charset=utf-8', body: '<h1>Guía externa</h1>' }),
    )

    await page.goto(`/resources/${resource.id}`)

    await expect(page).toHaveURL(url)
    await expect(page.getByRole('heading', { name: 'Guía externa' })).toBeVisible()
  })

  test('typing applies the search once 300 ms pass without keystrokes', async ({ page, seed }) => {
    const prefix = uniqueTitle('Recurso')
    const wanted = await seed.resource({ title: `${prefix} alfa` })
    const other = await seed.resource({ title: `${prefix} beta` })
    await page.clock.install()
    const resources = new CardListPage(page, '/resources')
    await resources.goto()
    await expect(resources.card(other.title)).toBeVisible()
    await page.clock.pauseAt(Date.now() + 1_000)

    await resources.search.fill(wanted.title)
    await page.clock.runFor(299)

    expect(resources.searches).toStrictEqual([])
    await page.clock.runFor(1)
    await expect.poll(() => resources.searches).toStrictEqual([wanted.title])
    await page.clock.resume()
    await expect(resources.card(wanted.title)).toBeVisible()
    await expect(resources.card(other.title)).toBeHidden()
  })

  test('Enter applies the search without waiting', async ({ page, seed }) => {
    const prefix = uniqueTitle('Recurso')
    const wanted = await seed.resource({ title: `${prefix} alfa` })
    const other = await seed.resource({ title: `${prefix} beta` })
    await page.clock.install()
    const resources = new CardListPage(page, '/resources')
    await resources.goto()
    await page.clock.pauseAt(Date.now() + 1_000)

    await resources.searchFor(wanted.title)

    await expect.poll(() => resources.searches).toStrictEqual([wanted.title])
    await page.clock.resume()
    await expect(resources.card(wanted.title)).toBeVisible()
    await expect(resources.card(other.title)).toBeHidden()
  })

  test('a search without matches says so', async ({ page, seed }) => {
    await seed.resource()
    const resources = new CardListPage(page, '/resources')
    await resources.goto()

    await resources.searchFor(uniqueTitle('Ningún recurso'))

    await expect(
      resources.main.getByText('No hay recursos que coincidan con tu búsqueda.'),
    ).toBeVisible()
    await expect(resources.main.getByRole('heading', { level: 3 })).toHaveCount(0)
  })

  test('an unknown resource is not found', async ({ page }) => {
    await page.goto('/resources/00000000-0000-4000-8000-000000000000')

    await expect(page.getByRole('main').getByText('No hemos encontrado ese recurso.')).toBeVisible()
    await expect(
      page.getByRole('main').getByRole('link', { name: 'Volver a recursos' }),
    ).toBeVisible()
  })
})
