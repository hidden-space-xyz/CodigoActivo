import { auditEntry } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { AdminShell } from '../../support/pages/admin-shell'
import { fullName } from '../../support/data/people'
import { DataTable } from '../../support/ui/data-table'

test.describe('audit log', () => {
  test('the sign-in of an account is found by its email and pinned by account and address', async ({
    pageAs,
    admin,
    accounts,
    clientAddress,
  }) => {
    const person = await accounts.actor({ label: 'audited' })
    const name = fullName(person.account)
    await auditEntry(
      admin.api,
      { search: person.account.email, action: 'CompleteLogin' },
      (entry) => entry.entityId === person.account.id,
    )
    const page = await pageAs(admin)
    await new AdminShell(page).open('Auditoría')
    const log = new DataTable(page)

    await log.search('Buscar por nombre o email', person.account.email)

    await expect(log.count).toHaveText('4 resultados')
    for (const action of ['Registrarse', 'Verificar la cuenta', 'Iniciar sesión']) {
      await expect(log.row(action)).toContainText(name)
    }
    const login = log.row('Completar el inicio de sesión')
    await expect(login).toContainText('Anónimo')
    await expect(login).toContainText(clientAddress)
    await expect(login).toContainText('Correcto')

    await login.getByRole('button', { name: `Filtrar por ${name}`, exact: true }).click()
    await log.search('Buscar por nombre o email', '')

    await expect(log.chips.getByText(`Entidad: ${name}`, { exact: true })).toBeVisible()
    await expect(log.count).toHaveText('4 resultados')

    await login.getByRole('button', { name: `Filtrar por la IP ${clientAddress}` }).click()

    await expect(log.chips.getByText(`IP: ${clientAddress}`, { exact: true })).toBeVisible()
    await expect(log.count).toHaveText('4 resultados')
  })

  test('pinning an actor, an entity and a trace narrows the log and leads to the diagnostics', async ({
    pageAs,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const entry = await auditEntry(
      admin.api,
      { action: 'CreateNewsItem', entityId: item.id },
      (found) => found.actorId === admin.account.id,
    )
    const traceId = entry.traceId ?? ''
    const actor = fullName(admin.account)
    const page = await pageAs(admin)
    await new AdminShell(page).open('Auditoría')
    const log = new DataTable(page)
    await log.search('Buscar por nombre o email', admin.account.email)
    const created = log.row(item.title)
    await expect(created).toContainText('Crear una novedad')
    await expect(created).toContainText('Correcto')
    await expect(created).toContainText(admin.account.email)

    await created.getByRole('button', { name: `Filtrar por ${actor}`, exact: true }).click()
    await log.search('Buscar por nombre o email', '')

    await expect(log.chips.getByText(`Quién: ${actor}`, { exact: true })).toBeVisible()
    await expect(created).toBeVisible()

    await created.getByRole('button', { name: `Filtrar por ${item.title}`, exact: true }).click()

    await expect(log.chips.getByText(`Entidad: ${item.title}`, { exact: true })).toBeVisible()
    await expect(log.count).toHaveText('1 resultado')
    await created.getByRole('button', { name: `Filtrar por la traza ${traceId}` }).click()

    await expect(log.chips.getByText(`Traza: ${traceId}`, { exact: true })).toBeVisible()
    await expect(page).toHaveURL(`/admin/audit?traceId=${traceId}`)

    await created.getByRole('button', { name: 'Ver el diagnóstico de esta traza' }).click()

    await expect(page).toHaveURL(`/admin/diagnostics?traceId=${traceId}`)
    await expect(new AdminShell(page).heading('Diagnóstico')).toBeVisible()
    await expect(new DataTable(page).chips.getByText(`Traza: ${traceId}`)).toBeVisible()
  })

  test('a wrong administrator password is logged as rejected with its reason', async ({
    pageAs,
    admin,
    participant,
  }) => {
    const target = participant.account
    await expect(
      admin.api.patch(`/api/users/${target.id}/admin`, {
        isAdmin: true,
        currentPassword: `${PASSWORD}-wrong`,
      }),
    ).rejects.toThrow('UserCurrentPasswordIncorrect')
    await auditEntry(
      admin.api,
      { action: 'SetAdmin', entityId: target.id, outcome: 'Rejected' },
      (entry) => entry.actorId === admin.account.id,
    )
    const page = await pageAs(admin)
    await new AdminShell(page).open('Auditoría')
    const log = new DataTable(page)
    await log.search('Buscar por nombre o email', target.email)

    await log.filterBy('Resultado', 'Rechazado')

    const refused = log.row('Cambiar los permisos de administrador').filter({
      hasText: fullName(admin.account),
    })
    await expect(refused).toContainText('Rechazado')
    await expect(refused).toContainText('La contraseña actual no es correcta.')
    await expect(refused).toContainText(fullName(target))
    await expect(log.chips.getByText('Resultado: Rechazado', { exact: true })).toBeVisible()
  })
})
