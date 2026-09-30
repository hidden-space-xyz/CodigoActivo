<script setup lang="ts">
import { watch } from 'vue'

import { EventCategoryFields } from '@/entities/event-category'
import { ActionButton } from '@/shared/ui/action-button'

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
  <el-dialog
    v-model="visible"
    :title="$t('pages.admin.events.categoryDialog.header')"
    width="min(380px, 92vw)"
    append-to-body
  >
    <form class="form" @submit.prevent="create">
      <EventCategoryFields
        v-model:name="draft.name"
        v-model:color="draft.color"
        :invalid="invalid('name')"
      />
      <small v-if="error" class="form__error">{{ error }}</small>
    </form>
    <template #footer>
      <ActionButton
        :label="$t('common.cancel')"
        text
        :disabled="creating"
        @click="visible = false"
      />
      <ActionButton
        :label="$t('common.create')"
        type="primary"
        :loading="creating"
        @click="create"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 6px;
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}
</style>
