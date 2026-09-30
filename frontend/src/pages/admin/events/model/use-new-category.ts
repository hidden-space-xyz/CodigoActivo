import { ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import {
  eventCategoryMutations,
  readEventCategoryDraft,
  toEventCategoryDraft,
} from '@/entities/event-category'
import { getErrorMessage } from '@/shared/lib/feedback'
import { useForm } from '@/shared/lib/form'

/**
 * Quick creation of an event category from the event dialog; a refused creation is reported inside
 * the dialog. Call it in `setup`.
 *
 * @param onCreated - Receives the id of the category just created.
 */
export function useNewCategory(onCreated: (categoryId: string) => void) {
  const { t } = useI18n()
  const form = useForm({ initial: () => toEventCategoryDraft(null), read: readEventCategoryDraft })
  const creation = useMutation(eventCategoryMutations.create())
  const error = ref('')

  function reset(): void {
    form.reset()
    error.value = ''
  }

  function create(): void {
    error.value = ''
    const input = form.submit()
    if (!input) return
    creation.mutate(input, {
      onSuccess: (created) => {
        onCreated(created.id)
      },
      onError: (failure) => {
        error.value = getErrorMessage(failure, t('pages.admin.events.categoryDialog.createError'))
      },
    })
  }

  return { form, creating: creation.isPending, error, reset, create }
}
