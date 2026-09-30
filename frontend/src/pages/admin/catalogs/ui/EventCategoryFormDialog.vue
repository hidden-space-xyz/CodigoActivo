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
import { ActionButton } from '@/shared/ui/action-button'

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
  <el-dialog
    v-model="visible"
    :title="
      category
        ? $t('pages.admin.catalogs.eventCategories.form.editHeader')
        : $t('pages.admin.catalogs.eventCategories.form.newHeader')
    "
    width="min(440px, 92vw)"
    append-to-body
  >
    <form class="form" @submit.prevent="save">
      <EventCategoryFields
        v-model:name="draft.name"
        v-model:color="draft.color"
        :invalid="invalid('name')"
      />
    </form>
    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
    </template>
  </el-dialog>
</template>

<style scoped>
.form {
  padding-top: 6px;
}
</style>
