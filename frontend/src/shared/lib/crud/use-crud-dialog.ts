import { computed, shallowRef } from 'vue'

/** Where the create/edit dialog of an admin list stands. */
export type CrudDialogState<TItem> =
  | { readonly mode: 'closed' }
  | { readonly mode: 'loading' }
  | { readonly mode: 'creating' }
  | { readonly mode: 'editing'; readonly item: TItem }

/** How an edit gets the item it opens with. */
interface CrudDialogOptions<TRow, TItem> {
  /** Loads the full item behind a row before editing; `null` when it no longer exists. */
  readonly load: (row: TRow) => Promise<TItem | null>
  /** Called when `load` finds nothing. */
  readonly onMissing?: (() => void) | undefined
  /** Called when `load` fails. */
  readonly onError?: ((error: unknown) => void) | undefined
}

/**
 * Create/edit dialog of an admin list as a small state machine. With `options`, opening an edit
 * loads the item first: while it loads new requests are ignored, and a missing or failed item
 * leaves the dialog closed. Without them the row itself is edited.
 */
export function useCrudDialog<TRow, TItem = TRow>(options?: CrudDialogOptions<TRow, TItem>) {
  const state = shallowRef<CrudDialogState<TItem>>({ mode: 'closed' })

  const editing = computed(() => (state.value.mode === 'editing' ? state.value.item : null))
  const loading = computed(() => state.value.mode === 'loading')
  const visible = computed({
    get: () => state.value.mode === 'creating' || state.value.mode === 'editing',
    set: (open: boolean) => {
      if (!open) close()
    },
  })

  function close(): void {
    state.value = { mode: 'closed' }
  }

  function openCreate(): void {
    if (state.value.mode === 'loading') return
    state.value = { mode: 'creating' }
  }

  async function openEdit(row: TRow): Promise<void> {
    if (state.value.mode === 'loading') return
    if (!options) {
      state.value = { mode: 'editing', item: row as unknown as TItem }
      return
    }
    state.value = { mode: 'loading' }
    try {
      const item = await options.load(row)
      state.value = item === null ? { mode: 'closed' } : { mode: 'editing', item }
      if (item === null) options.onMissing?.()
    } catch (error) {
      close()
      options.onError?.(error)
    }
  }

  return { state, editing, loading, visible, close, openCreate, openEdit }
}
