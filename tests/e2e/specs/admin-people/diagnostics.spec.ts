import { diagnosticsOfTrace, lockOutWithWrongPasswords } from '../../support/api/admin-people'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AdminShell } from '../../support/pages/admin-shell'
import { DataTable } from '../../support/ui/data-table'

const LOCKOUT_MESSAGE = 'An account was locked after 5 wrong passwords'
const LOCKOUT_EVENT = 'AccountLockoutLog'
const LOCKOUT_CATEGORY = `CodigoActivo.Application.Accounts.${LOCKOUT_EVENT}`

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
    await expect(shell.view('admin-diagnostics')).toBeVisible()
    const log = new DataTable(page)
    await expect(
      log.chip(t('pages.admin.diagnostics.filters.trace', { trace: traceId })),
    ).toBeVisible()
    const warning = t('pages.admin.diagnostics.levels.Warning')
    const lockout = log.row(LOCKOUT_MESSAGE)
    await expect(lockout).toContainText(warning)
    await expect(lockout).toContainText(LOCKOUT_EVENT)

    await log.filterBy('diagnostics-filter-level', 'Error')
    await expect(log.root.getByTestId('data-table-empty')).toHaveText(t('table.noMatches'))
    await log.filterBy('diagnostics-filter-level', 'Warning')
    await expect(
      log.chip(
        t('table.filterChip', {
          label: t('pages.admin.diagnostics.columns.level'),
          value: warning,
        }),
      ),
    ).toBeVisible()

    await lockout.getByTestId('row-action-detail').click()

    const detail = page.getByTestId('diagnostic-detail-dialog')
    await expect(detail).toContainText(LOCKOUT_MESSAGE)
    await expect(detail).toContainText(traceId)
    await expect(detail).toContainText(LOCKOUT_CATEGORY)
    await expect(detail.getByTestId('diagnostic-detail-exception')).toHaveText(
      t('pages.admin.diagnostics.detail.noException'),
    )
    await page.keyboard.press('Escape')
    await expect(detail).toBeHidden()

    await lockout.getByTestId('row-action-audit').click()

    await expect(page).toHaveURL(`/admin/audit?traceId=${traceId}`)
    const audit = new DataTable(page)
    await expect(audit.chip(t('pages.admin.audit.filters.trace', { trace: traceId }))).toBeVisible()
    await expect(audit.row(t('pages.admin.audit.actions.Login'))).toContainText(
      t('pages.admin.audit.outcomes.Rejected'),
    )
  })

  test('the trace filter lives in the address and its chip removes it', async ({
    pageAs,
    admin,
  }) => {
    const traceId = '0123456789abcdef0123456789abcdef'
    const page = await pageAs(admin)
    await page.goto(`/admin/diagnostics?traceId=${traceId.toUpperCase()}`)
    const log = new DataTable(page)
    const chip = t('pages.admin.diagnostics.filters.trace', { trace: traceId })
    await expect(log.chip(chip)).toHaveText(chip)
    await expect(log.root.getByTestId('data-table-empty')).toHaveText(t('table.noMatches'))

    await log.chip(chip).getByTestId('data-table-chip-remove').click()

    await expect(page).toHaveURL('/admin/diagnostics')
    await expect(log.chips).toBeHidden()
    await expect(log.root.getByTestId('diagnostics-filter-level')).toBeVisible()
  })
})
