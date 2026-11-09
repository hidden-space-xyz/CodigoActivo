import { describe, expect, it, vi } from 'vitest'

import { deleteAction, editAction } from '@/shared/ui/row-actions'

import { t } from '../../../../support/render'

describe('row actions', () => {
  it('edits a row with an inline pencil button, disabled while another row loads', () => {
    const run = vi.fn()

    expect(editAction(run)).toEqual({
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      disabled: false,
      run,
    })
    expect(editAction(run, true).disabled).toBe(true)
  })

  it('deletes a row with an inline trash button marked as destructive', () => {
    const run = vi.fn()

    expect(deleteAction(run)).toEqual({
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      inline: true,
      danger: true,
      run,
    })
  })
})
