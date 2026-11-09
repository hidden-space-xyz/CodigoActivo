<script setup lang="ts">
import { watch } from 'vue'

import {
  EventCategoryFields,
  readEventCategoryDraft,
  toEventCategoryDraft,
  type EventCategory,
  type EventCategoryInput,
} from '@/entities/event-category'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'

/** Opens the dialog; each time it opens the form is refilled from `category`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Category being edited; `null` opens the dialog in create mode. */
  category: EventCategory | null
  /** Shows a loading state on the save button while the parent persists the category. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid. */
  submit: [input: EventCategoryInput]
}>()

const form = useForm({ initial: () => toEventCategoryDraft(null), read: readEventCategoryDraft })
const { draft, invalid } = form

watch(visible, (open) => {
  if (open) form.reset(toEventCategoryDraft(props.category))
})

function save(): void {
  const value = form.submit()
  if (value) emit('submit', value)
}
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    data-testid="event-category-form-dialog"
    :title="
      category
        ? $t('pages.admin.catalogs.eventCategories.form.editHeader')
        : $t('pages.admin.catalogs.eventCategories.form.newHeader')
    "
    width="min(440px, 92vw)"
    append-to-body
    :saving="saving"
    @submit="save"
  >
    <EventCategoryFields
      v-model:name="draft.name"
      v-model:color="draft.color"
      :invalid="invalid('name')"
    />
  </FormDialog>
</template>
