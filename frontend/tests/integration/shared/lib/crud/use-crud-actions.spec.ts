import { ref } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { useCrudActions } from '@/shared/lib/crud'

import { acceptMessageBox, expectNotification, messageBox, textOf } from '../../../../support/dom'
import { mountComposable, t } from '../../../../support/render'

interface Row {
  readonly id: string
  readonly name: string
}

interface Callbacks {
  readonly onSuccess: () => void
  readonly onError: (error: unknown) => void
}

function mutation<TVariables>(outcome: 'success' | Error = 'success') {
  return {
    isPending: ref(false),
    mutate: vi.fn((_variables: TVariables, callbacks: Callbacks) => {
      if (outcome === 'success') callbacks.onSuccess()
      else callbacks.onError(outcome)
    }),
  }
}

async function mountActions(
  editing: Row | null,
  update = mutation<{ id: string; input: string }>(),
) {
  const dialog = { editing: ref(editing), close: vi.fn() }
  const create = mutation<string>()
  const remove = mutation<string>()
  const { result } = await mountComposable(() =>
    useCrudActions({
      dialog,
      create,
      update,
      remove,
      messages: {
        created: 'Partner created',
        updated: 'Partner updated',
        deleted: 'Partner deleted',
        deleteHeader: 'Delete partner',
        deleteMessage: (row: Row) => `Delete ${row.name}?`,
      },
    }),
  )
  return { ...result, dialog, create, update, remove }
}

describe('useCrudActions', () => {
  it('creates a new item, reports it and closes the dialog', async () => {
    const { save, create, update, dialog } = await mountActions(null)

    save('Acme')

    expect(create.mutate).toHaveBeenCalledWith('Acme', expect.anything())
    expect(update.mutate).not.toHaveBeenCalled()
    expect(dialog.close).toHaveBeenCalledOnce()
    await expectNotification('Partner created')
  })

  it('updates the item the dialog edits', async () => {
    const { save, create, update, dialog } = await mountActions({ id: 'p-1', name: 'Acme' })

    save('Acme Corp')

    expect(update.mutate).toHaveBeenCalledWith({ id: 'p-1', input: 'Acme Corp' }, expect.anything())
    expect(create.mutate).not.toHaveBeenCalled()
    expect(dialog.close).toHaveBeenCalledOnce()
    await expectNotification('Partner updated')
  })

  it('keeps the dialog open and reports the error when saving fails', async () => {
    const failing = mutation<{ id: string; input: string }>(new Error('Saving failed'))
    const { save, dialog } = await mountActions({ id: 'p-1', name: 'Acme' }, failing)

    save('Acme Corp')

    expect(dialog.close).not.toHaveBeenCalled()
    await expectNotification(t('errors.generic'))
  })

  it('tells whether a create or an update is under way', async () => {
    const { saving, create, update } = await mountActions(null)
    expect(saving.value).toBe(false)

    create.isPending.value = true
    expect(saving.value).toBe(true)

    create.isPending.value = false
    update.isPending.value = true
    expect(saving.value).toBe(true)
  })

  it('deletes a row only once the confirmation is accepted', async () => {
    const { confirmRemove, remove } = await mountActions(null)

    confirmRemove({ id: 'p-2', name: 'Initech' })
    await vi.waitFor(() => expect(textOf(messageBox())).toContain('Delete Initech?'))
    expect(remove.mutate).not.toHaveBeenCalled()

    await acceptMessageBox()

    await vi.waitFor(() => expect(remove.mutate).toHaveBeenCalledWith('p-2', expect.anything()))
    await expectNotification('Partner deleted')
  })
})
