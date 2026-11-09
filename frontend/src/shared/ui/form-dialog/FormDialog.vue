<script setup lang="ts">
import { ActionButton } from '@/shared/ui/action-button'

/** Opens the dialog; only its buttons close it, so a stray click or Escape keeps the draft. */
const visible = defineModel<boolean>('visible', { required: true })

defineProps<{
  /** Header of the dialog. */
  title: string
  /** CSS width of the dialog, such as `min(460px, 92vw)`. */
  width: string
  /** Shows a loading state on the confirm button and disables cancel while the parent saves. */
  saving: boolean
  /** Label of the confirm button; «Guardar» unless given. */
  confirmLabel?: string | undefined
  /** Height past which the form scrolls on wide screens, such as `68vh`; unlimited unless given. */
  scrollHeight?: string | undefined
}>()

const emit = defineEmits<{
  /** Fired when the form is sent, with Enter or with the confirm button. */
  submit: []
}>()
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="title"
    :width="width"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
  >
    <form
      class="form-dialog"
      :class="{ 'form-dialog--scroll': scrollHeight }"
      :style="scrollHeight ? { '--form-dialog-height': scrollHeight } : undefined"
      @submit.prevent="emit('submit')"
    >
      <slot />
    </form>
    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton
        :label="confirmLabel ?? $t('common.save')"
        type="primary"
        :loading="saving"
        data-testid="form-dialog-save"
        @click="emit('submit')"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.form-dialog {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 6px;
}

.form-dialog--scroll {
  max-height: var(--form-dialog-height);
  overflow-y: auto;
}

@media (max-width: 640px) {
  .form-dialog--scroll {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
