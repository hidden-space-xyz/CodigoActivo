import { describe, expect, it } from 'vitest'

import type { LegalPolicy } from '@/entities/legal-policy'
import LegalPolicyVersionDialog from '@/pages/admin/legal-policies/ui/LegalPolicyVersionDialog.vue'

import { richText } from '../../../../../support/builders'
import { dismissDialog, findButton, isDialogOpen, openDialog } from '../../../../../support/dom'
import { renderWithProviders, t } from '../../../../../support/render'

const POLICY: LegalPolicy = {
  kind: 'Privacy',
  version: 1,
  content: richText('Texto antiguo'),
  publishedAt: '2026-09-01T08:30:00Z',
}

function render(props: Record<string, unknown>) {
  return renderWithProviders(LegalPolicyVersionDialog, {
    props: { version: 1, policy: POLICY, loading: false, error: false, ...props },
    attach: true,
  })
}

describe('LegalPolicyVersionDialog', () => {
  const title = () => t('pages.admin.legalPolicies.versions.dialogTitle', { version: 1 })

  it('stays closed without a version', async () => {
    await render({ version: null, policy: undefined })

    expect(isDialogOpen(title())).toBe(false)
  })

  it('shows the version and loads it into the editor', async () => {
    const { wrapper } = await render({})

    const dialog = openDialog(title())
    expect(dialog.textContent).toContain('Texto antiguo')
    findButton(t('pages.admin.legalPolicies.versions.restore'), dialog).click()

    expect(wrapper.emitted('restore')).toEqual([[POLICY]])
  })

  it('closes when dismissed or with its close button', async () => {
    const { wrapper } = await render({})

    findButton(t('common.close'), openDialog(title())).click()
    await dismissDialog(wrapper, title())

    expect(wrapper.emitted('close')?.length).toBeGreaterThanOrEqual(2)
  })

  it('cannot load a version that has not arrived', async () => {
    await render({ policy: undefined, loading: true })

    const dialog = openDialog(title())
    expect(dialog.textContent).toContain(t('common.loading'))
    expect(findButton(t('pages.admin.legalPolicies.versions.restore'), dialog).disabled).toBe(true)
  })
})
