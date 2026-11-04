import { randomInt } from 'node:crypto'

import { dayFromToday } from '../../support/data/time'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { ContentAdminPage } from '../../support/pages/admin-content/content-admin-page'
import { dayText, pickerDate } from '../../support/data/formats'
import { pickerCell } from '../../support/pages/admin-content/date-picker'
import { confirm, fillPicker, notification } from '../../support/ui/element-plus'

test.describe('partner administration', () => {
  test('an administrator adds a partner whose website the table links to', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()
    const name = uniqueTitle('Patrocinador')
    const website = `https://www.example.org/${uniqueLetters()}`
    const since = dayFromToday(-30)

    const form = await partners.startCreate('Nuevo patrocinador')
    await form.field('Nombre').fill(name)
    await fillPicker(form.field('Fecha de alta'), pickerDate(since))
    await form.field('Nivel (tier)').fill('3')
    await form.field('Sitio web').fill(website)
    await form.pickImage()
    await form.saveAndClose()

    await expect(notification(page, 'Patrocinador creado.')).toBeVisible()
    await partners.search(name)
    const row = partners.table.row(name)
    await expect(row.getByRole('cell', { name: '3', exact: true })).toBeVisible()
    await expect(row).toContainText(dayText(since))
    const link = row.getByRole('link', { name: /example\.org\// })
    await expect(link).toHaveAttribute('href', website)
    await expect(link).toHaveAttribute('target', '_blank')
  })

  test('a partner needs a name, a start day and an image, and a valid website', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()
    const form = await partners.startCreate('Nuevo patrocinador')

    await form.save()
    await expect(form.root.getByText('El nombre es obligatorio.')).toBeVisible()
    await expect(form.field('Nombre')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.root.getByText('La fecha de alta es obligatoria.')).toBeVisible()
    await expect(form.root.getByText('La imagen es obligatoria.')).toBeVisible()

    await form.field('Nombre').fill(uniqueTitle('Patrocinador'))
    await form.field('Sitio web').fill('www.example.org')
    await form.save()
    await expect(form.root.getByText('El nombre es obligatorio.')).toBeHidden()
    await expect(
      form.root.getByText(
        'El sitio web debe ser una URL válida que empiece por http:// o https://.',
      ),
    ).toBeVisible()
    await expect(form.field('Sitio web')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.root).toBeVisible()
  })

  test('the start day cannot be later than today', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()
    const form = await partners.startCreate('Nuevo patrocinador')
    const since = form.field('Fecha de alta')

    await since.click()
    await pickerCell(page, dayFromToday(1)).click()
    await expect(since).toHaveValue('')

    await pickerCell(page, dayFromToday(0)).click()
    await expect(since).toHaveValue(pickerDate(dayFromToday(0)))
  })

  test('editing a partner saves its new name and tier', async ({ pageAs, admin, seed }) => {
    const partner = await seed.partner({ tier: 2 })
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()
    await partners.search(partner.name)
    const name = uniqueTitle('Patrocinador renombrado')

    await partners.table.rowAction(partner.name, 'Editar')
    const form = partners.form('Editar patrocinador')
    await expect(form.field('Nombre')).toHaveValue(partner.name)
    await expect(form.field('Nivel (tier)')).toHaveValue('2')
    await form.field('Nombre').fill(name)
    await form.field('Nivel (tier)').fill('5')
    await form.saveAndClose()

    await expect(notification(page, 'Patrocinador actualizado.')).toBeVisible()
    await partners.search(name)
    await expect(
      partners.table.row(name).getByRole('cell', { name: '5', exact: true }),
    ).toBeVisible()
  })

  test('deleting a partner asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const partner = await seed.partner()
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()
    await partners.search(partner.name)

    await partners.table.rowAction(partner.name, 'Eliminar')
    await confirm(page, 'Eliminar patrocinador', 'Eliminar')

    await expect(notification(page, 'Patrocinador eliminado.')).toBeVisible()
    await expect(partners.table.row(partner.name)).toHaveCount(0)
  })

  test('the name search and the tier column search narrow the list', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const word = uniqueLetters()
    const tier = randomInt(100_000, 1_000_000)
    const first = await seed.partner({ name: `Patrocinador ${word} uno`, tier })
    const second = await seed.partner({ name: `Patrocinador ${word} dos`, tier })
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'Patrocinadores')
    await partners.open()

    await partners.search(word)
    await partners.table.searchColumn('Nivel', String(tier))
    await expect(partners.table.chips).toContainText(`Nivel: ${tier}`)
    await expect(partners.table.row(first.name)).toBeVisible()
    await expect(partners.table.row(second.name)).toBeVisible()

    await partners.search(first.name)
    await expect(partners.table.row(first.name)).toBeVisible()
    await expect(partners.table.row(second.name)).toHaveCount(0)

    await partners.table.searchColumn('Nivel', String(tier + 1))
    await expect(partners.table.row(first.name)).toHaveCount(0)
    await expect(
      partners.table.root.getByText('Ningún resultado coincide con los filtros.'),
    ).toBeVisible()
  })
})
