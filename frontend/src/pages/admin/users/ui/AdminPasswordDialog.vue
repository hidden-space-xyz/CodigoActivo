<script setup lang="ts">
import { watch } from 'vue'

import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'

import { readAdminPasswordDraft, toAdminPasswordDraft } from '../model/admin-password-form'

/** Opens the dialog; the password is discarded whenever it opens or closes. */
const visible = defineModel<boolean>('visible', { required: true })

defineProps<{
  /** Dialog title naming the sensitive action. */
  title: string
  /** Explanation of what the action does to whom. */
  message: string
  /** Label of the button that authorizes the action. */
  confirmLabel: string
  /** `id` of the password input, unique on the page. */
  inputId: string
  /** Width of the dialog. */
  width: string
  /** Shows a loading state on the confirm button while the parent sends the action. */
  saving: boolean
  /** Rejection reported by the parent, such as an incorrect password; empty hides it. */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with the signed-in admin's password, never empty, to authorize the action. */
  submit: [currentPassword: string]
}>()

const form = useForm({ initial: toAdminPasswordDraft, read: readAdminPasswordDraft })
const { draft, errors, invalid } = form

watch(visible, () => {
  form.reset()
})

function confirm(): void {
  const password = form.submit()
  if (password) emit('submit', password)
}
</script>

<template>
  <el-dialog v-model="visible" :title="title" :width="width" :close-on-click-modal="false">
    <form class="form" @submit.prevent="confirm">
      <p class="form__message">{{ message }}</p>
      <div class="form__field">
        <label :for="inputId">{{ $t('pages.admin.users.adminPassword.label') }}</label>
        <el-input
          :id="inputId"
          v-model="draft.password"
          type="password"
          show-password
          autocomplete="current-password"
          :maxlength="128"
          :class="{ 'ca-invalid': invalid('password') || error }"
        />
        <small v-if="errors.password" class="form__error">{{ errors.password }}</small>
        <small v-else-if="error" class="form__error">{{ error }}</small>
      </div>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton :label="confirmLabel" type="primary" :loading="saving" @click="confirm" />
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

.form__message {
  margin: 0;
  line-height: 1.5;
  color: var(--ca-text);
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}
</style>
