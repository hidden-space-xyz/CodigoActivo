import { describe, expect, it, vi } from 'vitest'

import { useActionConfirm, useDeleteConfirm } from '@/shared/lib'

import { t } from '../../../support/render'

async function messageBox(): Promise<HTMLElement> {
  let found: HTMLElement | null = null
  await vi.waitFor(() => {
    found = document.body.querySelector<HTMLElement>('.el-message-box')
    expect(found).not.toBeNull()
  })
  return found as unknown as HTMLElement
}

function button(box: HTMLElement, label: string): HTMLButtonElement {
  const match = [...box.querySelectorAll<HTMLButtonElement>('button')].find(
    (candidate) => candidate.textContent?.trim() === label,
  )
  if (!match) throw new Error(`Button "${label}" not found`)
  return match
}

describe('useDeleteConfirm', () => {
  it('runs accept after the user confirms the deletion', async () => {
    const accept = vi.fn()
    const { confirmDelete } = useDeleteConfirm()

    confirmDelete({ header: 'Delete event', message: 'This cannot be undone', accept })

    const box = await messageBox()
    expect(box.textContent).toContain('Delete event')
    expect(box.textContent).toContain('This cannot be undone')
    const confirm = button(box, t('common.delete'))
    expect(confirm.classList.contains('el-button--danger')).toBe(true)

    confirm.click()

    await vi.waitFor(() => expect(accept).toHaveBeenCalledTimes(1))
  })

  it('does not run accept when the user cancels', async () => {
    const accept = vi.fn()
    const { confirmDelete } = useDeleteConfirm()

    confirmDelete({ header: 'Delete', message: 'Sure?', accept })
    const box = await messageBox()
    button(box, t('common.cancel')).click()

    await vi.waitFor(() => expect(box.isConnected && box.style.display !== 'none').toBe(false))
    expect(accept).not.toHaveBeenCalled()
  })
})

describe('useActionConfirm', () => {
  it('uses the caller label without the danger style', async () => {
    const accept = vi.fn()
    const { confirmAction } = useActionConfirm()

    confirmAction({ header: 'Publish', message: 'Publish now?', acceptLabel: 'Publish it', accept })

    const box = await messageBox()
    const confirm = button(box, 'Publish it')
    expect(confirm.classList.contains('el-button--danger')).toBe(false)

    confirm.click()

    await vi.waitFor(() => expect(accept).toHaveBeenCalledTimes(1))
  })
})
