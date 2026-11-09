import { computed, type Ref } from 'vue'

import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'

/** What a mutation reports once it settles. */
interface MutateCallbacks {
  readonly onSuccess: () => void
  readonly onError: (error: unknown) => void
}

/** A mutation as `useMutation` returns it, reduced to what the actions use. */
interface Mutation<TVariables> {
  readonly isPending: Readonly<Ref<boolean>>
  mutate(variables: TVariables, callbacks: MutateCallbacks): void
}

/** Texts the actions report with, already translated. */
interface CrudMessages<TRow> {
  readonly created: string
  readonly updated: string
  readonly deleted: string
  readonly deleteHeader: string
  readonly deleteMessage: (row: TRow) => string
}

/** The dialog an admin list edits in and the mutations that write its items. */
interface CrudActionsOptions<TItem, TRow, TInput> {
  readonly dialog: { readonly editing: Readonly<Ref<TItem | null>>; readonly close: () => void }
  readonly create: Mutation<TInput>
  readonly update: Mutation<{ id: string; input: TInput }>
  readonly remove: Mutation<string>
  readonly messages: CrudMessages<TRow>
}

/**
 * Writes of an admin list: `save` creates an item, or updates the one the dialog edits, and closes
 * the dialog once it succeeds; `confirmRemove` asks before deleting a row. Each outcome is reported
 * with a toast. Call it in `setup`.
 */
export function useCrudActions<
  TItem extends { readonly id: string },
  TRow extends { readonly id: string },
  TInput,
>(options: CrudActionsOptions<TItem, TRow, TInput>) {
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()
  const { dialog, create, update, remove, messages } = options
  const saving = computed(() => create.isPending.value || update.isPending.value)

  function save(input: TInput): void {
    const item = dialog.editing.value
    if (item) {
      update.mutate({ id: item.id, input }, feedback.outcome(messages.updated, dialog.close))
      return
    }
    create.mutate(input, feedback.outcome(messages.created, dialog.close))
  }

  function confirmRemove(row: TRow): void {
    confirmDelete({
      header: messages.deleteHeader,
      message: messages.deleteMessage(row),
      accept: () => {
        remove.mutate(row.id, feedback.outcome(messages.deleted))
      },
    })
  }

  return { saving, save, confirmRemove }
}
