<script setup lang="ts">
import { watch } from 'vue'

import { EventCategoryFields } from '@/entities/event-category'
import { FormDialog } from '@/shared/ui/form-dialog'

import { useNewCategory } from '../model/use-new-category'

/** Opens the dialog with a blank category each time. */
const visible = defineModel<boolean>('visible', { required: true })

const emit = defineEmits<{
  /** Fired with the id of the category just created, before the dialog closes. */
  created: [categoryId: string]
}>()

const { form, creating, error, reset, create } = useNewCategory((categoryId) => {
  emit('created', categoryId)
  visible.value = false
})
const { draft, invalid } = form

watch(visible, (open) => {
  if (open) reset()
})
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    :title="$t('pages.admin.events.categoryDialog.header')"
    width="min(380px, 92vw)"
    append-to-body
    data-testid="new-category-dialog"
    :saving="creating"
    :confirm-label="$t('common.create')"
    @submit="create"
  >
    <EventCategoryFields
      v-model:name="draft.name"
      v-model:color="draft.color"
      :invalid="invalid('name')"
    />
    <p v-if="error" class="ca-form-alert" role="alert">{{ error }}</p>
  </FormDialog>
</template>
