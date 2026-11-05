import { diagnosticsOfTrace, lockOutWithWrongPasswords } from '../../support/api/admin-people'
import { expect, test } from '../../support/fixtures/test'
import { AdminShell } from '../../support/pages/admin-shell'
import { DataTable } from '../../support/ui/data-table'

const LOCKOUT_MESSAGE = 'An account was locked after 5 wrong passwords'

test.describe('diagnostic log', () => {
  test('a password lockout is logged as a warning with its detail and its trace', async ({
    pageAs,
    initialAdmin,
    accounts,
    clientAddress,
  }) => {
    const account = await accounts.create({ label: 'locked' })
    const traceId = await lockOutWithWrongPasswords(clientAddress, account.email)
    await diagnosticsOfTrace(initialAdmin.api, traceId)
    const page = await pageAs(initialAdmin)

    await page.goto(`/admin/diagnostics?traceId=${traceId}`)

    const shell = new AdminShell(page)
    await expect(shell.heading('Diagnóstico')).toBeVisible()
    const log = new DataTable(page)
    await expect(log.chips.getByText(`Traza: ${traceId}`, { exact: true })).toBeVisible()
    const lockout = log.row(LOCKOUT_MESSAGE)
    await expect(lockout).toContainText('Aviso')
    await expect(lockout).toContainText('AccountLockoutLog')

    await log.filterBy('Nivel', 'Error')
    await expect(log.root.getByText('Ningún resultado coincide con los filtros.')).toBeVisible()
    await log.filterBy('Nivel', 'Aviso')
    await expect(log.chips.getByText('Nivel: Aviso', { exact: true })).toBeVisible()

    await lockout.getByRole('button', { name: 'Ver detalle' }).click()

    const detail = page.getByRole('dialog', { name: 'Detalle de la entrada' })
    await expect(detail).toContainText(LOCKOUT_MESSAGE)
    await expect(detail).toContainText(traceId)
    await expect(detail).toContainText('CodigoActivo.Application.Accounts.AccountLockoutLog')
    await expect(detail.getByText('Esta entrada no tiene excepción.')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(detail).toBeHidden()

    await lockout.getByRole('button', { name: 'Ver la auditoría de esta traza' }).click()

    await expect(page).toHaveURL(`/admin/audit?traceId=${traceId}`)
    const audit = new DataTable(page)
    await expect(audit.chips.getByText(`Traza: ${traceId}`, { exact: true })).toBeVisible()
    await expect(audit.row('Iniciar sesión')).toContainText('Rechazado')
  })

  test('the trace filter lives in the address and its chip removes it', async ({
    pageAs,
    admin,
  }) => {
    const traceId = '0123456789abcdef0123456789abcdef'
    const page = await pageAs(admin)
    await page.goto(`/admin/diagnostics?traceId=${traceId.toUpperCase()}`)
    const log = new DataTable(page)
    const chip = `Traza: ${traceId}`
    await expect(log.chips.getByText(chip, { exact: true })).toBeVisible()
    await expect(log.root.getByText('Ningún resultado coincide con los filtros.')).toBeVisible()

    await log.chips.getByRole('button', { name: `Quitar el filtro «${chip}»` }).click()

    await expect(page).toHaveURL('/admin/diagnostics')
    await expect(log.chips).toBeHidden()
    await expect(log.root.getByRole('button', { name: 'Filtrar por Nivel' })).toBeVisible()
  })
})
