<script setup lang="ts">
import { watch } from 'vue'

import { PASSWORD_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'

import { readAdminPasswordDraft, toAdminPasswordDraft } from '../model/admin-password-form'

/** Opens the dialog; the password is discarded whenever it opens or closes. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Dialog title naming the sensitive action. */
  title: string
  /** Explanation of what the action does to whom. */
  message: string
  /** Label of the button that authorizes the action. */
  confirmLabel: string
  /** `id` of the password input, unique on the page. */
  inputId: string
  /** CSS width of the dialog, such as `min(440px, 92vw)`. */
  width: string
  /** Shows a loading state on the confirm button while the parent sends the action. */
  saving: boolean
  /** Rejection reported by the parent, such as an incorrect password; empty hides it. */
  error: string
  /** Hides the password field for a choice that needs none; `submit` then carries `''`. */
  passwordless?: boolean
}>()

const emit = defineEmits<{
  /** Fired with the signed-in admin's password, empty only when `passwordless`. */
  submit: [currentPassword: string]
}>()

defineSlots<{
  /** Fields of the action shown above the password, such as the choice it applies. */
  default?: () => unknown
}>()

const form = useForm({ initial: toAdminPasswordDraft, read: readAdminPasswordDraft })
const { draft, errors, invalid } = form

watch(visible, () => {
  form.reset()
})

function confirm(): void {
  if (props.passwordless) {
    emit('submit', '')
    return
  }
  const password = form.submit()
  if (password) emit('submit', password)
}
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    :title="title"
    :width="width"
    :saving="saving"
    :confirm-label="confirmLabel"
    @submit="confirm"
  >
    <p class="form__message">{{ message }}</p>
    <slot />
    <FormField
      v-if="!passwordless"
      v-slot="{ control }"
      :for="inputId"
      :label="$t('common.yourPassword')"
      :invalid="invalid('password')"
      :error="errors.password || error"
      error-testid="admin-password-error"
    >
      <el-input
        v-model="draft.password"
        v-bind="control"
        data-testid="admin-password-input"
        type="password"
        show-password
        autocomplete="current-password"
        :maxlength="PASSWORD_MAX_LENGTH"
      />
    </FormField>
  </FormDialog>
</template>

<style scoped>
.form__message {
  margin: 0;
  line-height: 1.5;
  color: var(--ca-text);
}
</style>
