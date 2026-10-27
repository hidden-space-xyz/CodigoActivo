import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { accountMutations, accountQueries, type AccountHistoryEntry } from '@/entities/account'
import { useSession } from '@/entities/session'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib/feedback'

/**
 * Household's event participation history split into upcoming and past entries, plus the mutations
 * that save and delete the account's rating of an event. Both refetch the history, which carries
 * the stored rating of each event, so the form reopens with it. Call it in `setup`.
 */
export function useAccountHistory() {
  const { t } = useI18n()
  const session = useSession()
  const feedback = useCrudFeedback()
  const { confirmDelete } = useDeleteConfirm()

  const history = useQuery(() => ({
    ...accountQueries.history(),
    enabled: session.user !== null,
  }))
  const entries = computed(() => history.data.value ?? [])
  const upcoming = computed(() => entries.value.filter((entry) => !entry.isPast))
  const past = computed(() => entries.value.filter((entry) => entry.isPast))

  const saveRating = useMutation(accountMutations.rateEvent())
  const deleteRating = useMutation(accountMutations.deleteRating())

  /** Asks for confirmation and deletes the account's rating of `entry`; `then` runs once it is gone. */
  function removeRating(entry: AccountHistoryEntry, then: () => void): void {
    confirmDelete({
      header: t('pages.account.history.dialog.deleteHeader'),
      message: t('pages.account.history.dialog.deleteMessage', { event: entry.title }),
      accept: () => {
        deleteRating.mutate(
          entry.eventId,
          feedback.outcome(t('pages.account.history.deletedDetail'), then),
        )
      },
    })
  }

  return { history, entries, upcoming, past, saveRating, deleteRating, removeRating }
}
