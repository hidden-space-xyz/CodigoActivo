import { minorsOf } from '../../support/api/my-account'
import { fullName, newMinor } from '../../support/data/people'
import { birthDateForAge, dayFromToday } from '../../support/data/time'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AccountPage } from '../../support/pages/account/account-page'
import { MinorDialog, MinorsSection } from '../../support/pages/account/minors-section'
import { GENDER_NAMES } from '../../support/pages/registration-page'
import { notification } from '../../support/ui/element-plus'

test.describe('minors in my care', () => {
  test('a person adds, edits and removes a minor in their care', async ({ accounts, pageAs }) => {
    const actor = await accounts.actor({ label: 'minors' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const minors = new MinorsSection(account)
    await expect(minors.empty).toBeVisible()

    await minors.add.click()
    const addDialog = new MinorDialog(page, 'add')
    const minor = newMinor(actor.account, 9)
    await expect(addDialog.root.getByTestId('minor-dialog-declaration')).toHaveText(
      t('pages.account.minors.declaration'),
    )
    await addDialog.fill({ ...minor, birthDate: birthDateForAge(18) })
    await expect(addDialog.birthDate).toHaveAttribute('max', dayFromToday(0))
    await expect(addDialog.birthDateOutOfRange).toBeVisible()
    await addDialog.save.click()
    await expect(addDialog.root).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toHaveLength(0)

    await addDialog.birthDate.fill(minor.birthDate)
    await expect(addDialog.birthDateOutOfRange).toBeHidden()
    await addDialog.save.click()

    await expect(notification(page, t('pages.account.minors.addedSummary'))).toBeVisible()
    const name = fullName(minor)
    await expect(minors.minor(name)).toContainText(GENDER_NAMES[minor.gender])
    await expect(minors.empty).toBeHidden()
    const [added] = await minorsOf(actor.api, actor.account.id)
    expect(added).toMatchObject({
      firstName: minor.firstName,
      lastName: minor.lastName,
      birthDate: minor.birthDate,
      gender: minor.gender,
      parentId: actor.account.id,
    })

    await minors.minor(name).getByTestId('minors-item-edit').click()
    const editDialog = new MinorDialog(page, 'edit')
    await expect(editDialog.firstName).toHaveValue(minor.firstName)
    await expect(editDialog.birthDate).toHaveValue(minor.birthDate)
    await editDialog.firstName.fill('Candela')
    await editDialog.save.click()

    await expect(notification(page, t('pages.account.minors.updatedSummary'))).toBeVisible()
    const renamed = fullName({ ...minor, firstName: 'Candela' })
    await expect(minors.minor(renamed)).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toStrictEqual([
      expect.objectContaining({ id: added?.id, firstName: 'Candela', birthDate: minor.birthDate }),
    ])

    await minors.minor(renamed).getByTestId('minors-item-delete').click()
    const deleteDialog = page.getByTestId('minor-delete-dialog')
    await expect(deleteDialog).toContainText(
      t('pages.account.minors.deleteConfirm', { name: renamed }),
    )
    await deleteDialog.getByTestId('minor-delete-dialog-confirm').click()
    await expect(deleteDialog).toBeHidden()

    await expect(notification(page, t('pages.account.minors.deletedSummary'))).toBeVisible()
    await expect(minors.empty).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toHaveLength(0)
  })
})
