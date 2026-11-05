import { auditEntry } from '../../support/api/admin-people'
import { PASSWORD } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { AdminShell } from '../../support/pages/admin-shell'
import { fullName } from '../../support/data/people'
import { DataTable } from '../../support/ui/data-table'

test.describe('audit log', () => {
  test('the sign-in of an account is found by its email and pinned by account and address', async ({
    pageAs,
    initialAdmin,
    accounts,
    clientAddress,
  }) => {
    const person = await accounts.actor({ label: 'audited' })
    const name = fullName(person.account)
    await auditEntry(
      initialAdmin.api,
      { search: person.account.email, action: 'CompleteLogin' },
      (entry) => entry.entityId === person.account.id,
    )
    const page = await pageAs(initialAdmin)
    await new AdminShell(page).open('admin-audit')
    const log = new DataTable(page)

    await log.search(person.account.email)

    await expect(log.count).toHaveText(t('table.results', { n: 4 }))
    for (const action of [
      t('pages.admin.audit.actions.Register'),
      t('pages.admin.audit.actions.VerifyUser'),
      t('pages.admin.audit.actions.Login'),
    ]) {
      await expect(log.row(action)).toContainText(name)
    }
    const login = log.row(t('pages.admin.audit.actions.CompleteLogin'))
    await expect(login).toContainText(t('pages.admin.audit.actorTypes.Anonymous'))
    await expect(login).toContainText(clientAddress)
    await expect(login).toContainText(t('pages.admin.audit.outcomes.Succeeded'))

    await login.getByTestId('audit-entity').click()
    await log.search('')

    await expect(log.chip(t('pages.admin.audit.filters.entity', { entity: name }))).toBeVisible()
    await expect(log.count).toHaveText(t('table.results', { n: 4 }))

    await login.getByTestId('audit-ip').click()

    await expect(log.chip(t('pages.admin.audit.filters.ip', { ip: clientAddress }))).toBeVisible()
    await expect(log.count).toHaveText(t('table.results', { n: 4 }))
  })

  test('pinning an actor, an entity and a trace narrows the log and leads to the diagnostics', async ({
    pageAs,
    initialAdmin,
    admin,
    seed,
  }) => {
    const item = await seed.newsItem()
    const entry = await auditEntry(
      initialAdmin.api,
      { action: 'CreateNewsItem', entityId: item.id },
      (found) => found.actorId === admin.account.id,
    )
    const traceId = entry.traceId ?? ''
    const actor = fullName(admin.account)
    const page = await pageAs(initialAdmin)
    await new AdminShell(page).open('admin-audit')
    const log = new DataTable(page)
    await log.search(admin.account.email)
    const created = log.row(item.title)
    await expect(created).toContainText(t('pages.admin.audit.actions.CreateNewsItem'))
    await expect(created).toContainText(t('pages.admin.audit.outcomes.Succeeded'))
    await expect(created).toContainText(admin.account.email)

    await created.getByTestId('audit-actor').click()
    await log.search('')

    await expect(log.chip(t('pages.admin.audit.filters.actor', { actor }))).toBeVisible()
    await expect(created).toBeVisible()

    await created.getByTestId('audit-entity').click()

    await expect(
      log.chip(t('pages.admin.audit.filters.entity', { entity: item.title })),
    ).toBeVisible()
    await expect(log.count).toHaveText(t('table.results', { n: 1 }))
    await created.getByTestId('audit-trace').click()

    await expect(log.chip(t('pages.admin.audit.filters.trace', { trace: traceId }))).toBeVisible()
    await expect(page).toHaveURL(`/admin/audit?traceId=${traceId}`)

    await created.getByTestId('row-action-diagnostics').click()

    await expect(page).toHaveURL(`/admin/diagnostics?traceId=${traceId}`)
    await expect(new AdminShell(page).view('admin-diagnostics')).toBeVisible()
    await expect(
      new DataTable(page).chip(t('pages.admin.diagnostics.filters.trace', { trace: traceId })),
    ).toBeVisible()
  })

  test('a wrong superadministrator password is logged as rejected with its reason', async ({
    pageAs,
    initialAdmin,
    participant,
  }) => {
    const target = participant.account
    await expect(
      initialAdmin.api.patch(`/api/users/${target.id}/staff-role`, {
        staffRole: 'Administrator',
        currentPassword: `${PASSWORD}-wrong`,
      }),
    ).rejects.toThrow('UserCurrentPasswordIncorrect')
    await auditEntry(
      initialAdmin.api,
      { action: 'SetStaffRole', entityId: target.id, outcome: 'Rejected' },
      (entry) => entry.actorId === initialAdmin.account.id,
    )
    const page = await pageAs(initialAdmin)
    await new AdminShell(page).open('admin-audit')
    const log = new DataTable(page)
    await log.search(target.email)

    await log.filterBy('audit-filter-outcome', 'Rejected')

    const rejected = t('pages.admin.audit.outcomes.Rejected')
    const refused = log.row(t('pages.admin.audit.actions.SetStaffRole')).filter({
      hasText: fullName(initialAdmin.account),
    })
    await expect(refused).toContainText(rejected)
    await expect(refused).toContainText(t('errors.UserCurrentPasswordIncorrect'))
    await expect(refused).toContainText(fullName(target))
    await expect(
      log.chip(
        t('table.filterChip', { label: t('pages.admin.audit.columns.outcome'), value: rejected }),
      ),
    ).toBeVisible()
  })
})
