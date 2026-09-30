import { describe, expect, it, vi } from 'vitest'

import { useCrudDialog } from '@/shared/lib/crud'

interface Row {
  readonly id: string
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (error: unknown) => void
  const promise = new Promise<T>((onResolve, onReject) => {
    resolve = onResolve
    reject = onReject
  })
  return { promise, resolve, reject }
}

describe('useCrudDialog', () => {
  it('opens empty for a new item and closes again', () => {
    const dialog = useCrudDialog<Row>()

    dialog.openCreate()
    expect(dialog.visible.value).toBe(true)
    expect(dialog.editing.value).toBeNull()

    dialog.visible.value = false
    expect(dialog.state.value).toEqual({ mode: 'closed' })
  })

  it('edits the row itself when it has nothing to load', async () => {
    const dialog = useCrudDialog<Row>()

    await dialog.openEdit({ id: 'row-1' })

    expect(dialog.visible.value).toBe(true)
    expect(dialog.editing.value).toEqual({ id: 'row-1' })
  })

  it('loads the full item before editing and ignores other requests meanwhile', async () => {
    const pending = deferred<{ id: string; title: string } | null>()
    const dialog = useCrudDialog<Row, { id: string; title: string }>({
      load: () => pending.promise,
    })

    const opening = dialog.openEdit({ id: 'row-1' })
    expect(dialog.loading.value).toBe(true)
    expect(dialog.visible.value).toBe(false)
    dialog.openCreate()
    await dialog.openEdit({ id: 'row-2' })

    pending.resolve({ id: 'row-1', title: 'Loaded' })
    await opening

    expect(dialog.editing.value).toEqual({ id: 'row-1', title: 'Loaded' })
  })

  it('stays closed and reports a missing item or a failed load', async () => {
    const onMissing = vi.fn()
    const onError = vi.fn()
    const failure = new Error('offline')
    const load = vi
      .fn<(row: Row) => Promise<Row | null>>()
      .mockResolvedValueOnce(null)
      .mockRejectedValueOnce(failure)
    const dialog = useCrudDialog<Row>({ load, onMissing, onError })

    await dialog.openEdit({ id: 'gone' })
    await dialog.openEdit({ id: 'broken' })

    expect(dialog.visible.value).toBe(false)
    expect(dialog.loading.value).toBe(false)
    expect(onMissing).toHaveBeenCalledOnce()
    expect(onError).toHaveBeenCalledWith(failure)
  })
})
