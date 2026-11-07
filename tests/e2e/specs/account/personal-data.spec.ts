import { fullName } from '../../support/data/people'
import { dayFromToday } from '../../support/data/time'
import { expect, test } from '../../support/fixtures/test'
import { AccountPage } from '../../support/pages/account/account-page'

test.describe('personal data', () => {
  test('a guardian downloads a copy of the household data as JSON', async ({
    pageAs,
    guardian,
  }) => {
    const person = guardian.account
    const account = new AccountPage(await pageAs(guardian))
    await account.goto()

    const { name, data } = await account.downloadPersonalData()

    expect(name).toBe(`codigo-activo-mis-datos-${dayFromToday(0)}.json`)
    expect(data.cuenta).toMatchObject({
      email: person.email,
      nombre: person.firstName,
      apellidos: person.lastName,
    })
    expect(
      data.menores.map((minor) => `${minor.nombre} ${minor.apellidos} ${minor.fechaDeNacimiento}`),
    ).toStrictEqual(
      expect.arrayContaining(
        person.dependents.map((minor) => `${fullName(minor)} ${minor.birthDate}`),
      ),
    )
    expect(data.menores).toHaveLength(person.dependents.length)
  })
})
