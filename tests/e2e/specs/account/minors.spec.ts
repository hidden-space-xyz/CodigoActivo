import { minorsOf } from '../../support/api/account'
import { newMinor } from '../../support/data/people'
import { birthDateForAge, dayFromToday } from '../../support/data/time'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'
import { MinorDialog, MinorsSection } from '../../support/pages/account/minors-section'
import { GENDER_NAMES } from '../../support/pages/registration-page'
import { confirm, notification } from '../../support/ui/element-plus'

test.describe('minors in my care', () => {
  test('a person adds, edits and removes a minor in their care', async ({ accounts, pageAs }) => {
    const actor = await accounts.actor({ label: 'minors' })
    const page = await pageAs(actor)
    const account = new AccountPage(page)
    await account.goto()
    const minors = new MinorsSection(account)
    await expect(minors.empty).toBeVisible()

    await minors.add.click()
    const addDialog = new MinorDialog(page, 'Añadir menor')
    const minor = newMinor(actor.account, 9)
    await expect(
      addDialog.root.getByText('Al añadirlo declaras ser su padre, madre o tutor legal.'),
    ).toBeVisible()
    await addDialog.fill({ ...minor, birthDate: birthDateForAge(18) })
    await expect(addDialog.birthDate).toHaveAttribute('max', dayFromToday(0))
    await expect(addDialog.birthDateOutOfRange).toBeVisible()
    await addDialog.save.click()
    await expect(addDialog.root).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toHaveLength(0)

    await addDialog.birthDate.fill(minor.birthDate)
    await expect(addDialog.birthDateOutOfRange).toBeHidden()
    await addDialog.save.click()

    await expect(notification(page, 'Menor añadido')).toBeVisible()
    const name = `${minor.firstName} ${minor.lastName}`
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

    await minors.minor(name).getByRole('button', { name: 'Editar', exact: true }).click()
    const editDialog = new MinorDialog(page, 'Editar menor')
    await expect(editDialog.firstName).toHaveValue(minor.firstName)
    await expect(editDialog.birthDate).toHaveValue(minor.birthDate)
    await editDialog.firstName.fill('Candela')
    await editDialog.save.click()

    await expect(notification(page, 'Menor actualizado')).toBeVisible()
    const renamed = `Candela ${minor.lastName}`
    await expect(minors.minor(renamed)).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toStrictEqual([
      expect.objectContaining({ id: added?.id, firstName: 'Candela', birthDate: minor.birthDate }),
    ])

    await minors.minor(renamed).getByRole('button', { name: 'Eliminar', exact: true }).click()
    await expect(page.getByRole('dialog', { name: 'Eliminar menor' })).toContainText(
      `¿Seguro que quieres eliminar a ${renamed}?`,
    )
    await confirm(page, 'Eliminar menor', 'Eliminar')

    await expect(notification(page, 'Menor eliminado')).toBeVisible()
    await expect(minors.empty).toBeVisible()
    expect(await minorsOf(actor.api, actor.account.id)).toHaveLength(0)
  })
})
