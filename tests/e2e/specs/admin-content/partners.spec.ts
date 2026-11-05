import { randomInt } from 'node:crypto'

import { dayFromToday } from '../../support/data/time'
import { uniqueLetters, uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
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
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()
    const name = uniqueTitle('Patrocinador')
    const website = `https://www.example.org/${uniqueLetters()}`
    const since = dayFromToday(-30)

    const form = await partners.startCreate('partner-form-dialog')
    await form.field('partner-form-name').fill(name)
    await fillPicker(form.field('partner-form-from-date'), pickerDate(since))
    await form.input('partner-form-tier').fill('3')
    await form.field('partner-form-website').fill(website)
    await form.pickImage()
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.partners.toasts.created'))).toBeVisible()
    await partners.search(name)
    const row = partners.table.row(name)
    await expect(row.getByTestId('admin-partners-tier')).toHaveText('3')
    await expect(row).toContainText(dayText(since))
    const link = row.getByTestId('admin-partners-website')
    await expect(link).toHaveAttribute('href', website)
    await expect(link).toHaveAttribute('target', '_blank')
  })

  test('a partner needs a name, a start day and an image, and a valid website', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()
    const form = await partners.startCreate('partner-form-dialog')

    await form.save()
    await expect(form.field('partner-form-name-error')).toHaveText(
      t('pages.admin.partners.form.problems.nameRequired'),
    )
    await expect(form.field('partner-form-name')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.field('partner-form-from-date-error')).toHaveText(
      t('pages.admin.partners.form.problems.fromDateRequired'),
    )
    await expect(form.field('thumbnail-field')).toContainText(t('common.imageRequired'))

    await form.field('partner-form-name').fill(uniqueTitle('Patrocinador'))
    await form.field('partner-form-website').fill('www.example.org')
    await form.save()
    await expect(form.field('partner-form-name-error')).toBeHidden()
    await expect(form.field('partner-form-website-error')).toHaveText(
      t('pages.admin.partners.form.problems.websiteInvalid'),
    )
    await expect(form.field('partner-form-website')).toHaveAttribute('aria-invalid', 'true')
    await expect(form.root).toBeVisible()
  })

  test('the start day cannot be later than today', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()
    const form = await partners.startCreate('partner-form-dialog')
    const since = form.input('partner-form-from-date')

    await since.click()
    await pickerCell(page, dayFromToday(1)).click()
    await expect(since).toHaveValue('')

    await pickerCell(page, dayFromToday(0)).click()
    await expect(since).toHaveValue(pickerDate(dayFromToday(0)))
  })

  test('editing a partner saves its new name and tier', async ({ pageAs, admin, seed }) => {
    const partner = await seed.partner({ tier: 2 })
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()
    await partners.search(partner.name)
    const name = uniqueTitle('Patrocinador renombrado')

    await partners.table.rowAction(partner.name, 'edit')
    const form = partners.form('partner-form-dialog')
    await expect(form.field('partner-form-name')).toHaveValue(partner.name)
    await expect(form.input('partner-form-tier')).toHaveValue('2')
    await form.field('partner-form-name').fill(name)
    await form.input('partner-form-tier').fill('5')
    await form.saveAndClose()

    await expect(notification(page, t('pages.admin.partners.toasts.updated'))).toBeVisible()
    await partners.search(name)
    await expect(partners.table.row(name).getByTestId('admin-partners-tier')).toHaveText('5')
  })

  test('deleting a partner asks for confirmation and removes it', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const partner = await seed.partner()
    const page = await pageAs(admin)
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()
    await partners.search(partner.name)

    await partners.table.rowAction(partner.name, 'delete')
    await confirm(page)

    await expect(notification(page, t('pages.admin.partners.toasts.deleted'))).toBeVisible()
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
    const partners = new ContentAdminPage(page, 'admin-partners')
    await partners.open()

    await partners.search(word)
    await partners.table.searchColumn('admin-partners-tier-search', String(tier))
    await expect(partners.table.chips).toContainText(
      t('table.filterChip', { label: t('pages.admin.partners.columns.tier'), value: tier }),
    )
    await expect(partners.table.row(first.name)).toBeVisible()
    await expect(partners.table.row(second.name)).toBeVisible()

    await partners.search(first.name)
    await expect(partners.table.row(first.name)).toBeVisible()
    await expect(partners.table.row(second.name)).toHaveCount(0)

    await partners.table.searchColumn('admin-partners-tier-search', String(tier + 1))
    await expect(partners.table.row(first.name)).toHaveCount(0)
    await expect(partners.table.root).toContainText(t('table.noMatches'))
  })
})
