<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'

import type { User } from '@/entities/user'
import { fullName } from '@/shared/lib'

const props = defineProps<{
  /** Opens the dialog; the password is discarded whenever it opens or closes. */
  visible: boolean
  /** User whose second factor returns to email; only their name is shown. */
  user: User | null
  /** Shows a loading state on the confirm button while the parent sends the reset. */
  saving: boolean
  /** Rejection reported by the parent, such as an incorrect password; empty hides it. */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with `false` when the dialog is cancelled or dismissed. */
  'update:visible': [value: boolean]
  /** Fired with the signed-in admin's password, never empty, to authorize the reset. */
  submit: [currentPassword: string]
}>()

const password = ref('')
const submitted = ref(false)
const missing = computed(() => submitted.value && !password.value)
const targetName = computed(() => fullName(props.user ?? {}))

watch(
  () => props.visible,
  () => {
    password.value = ''
    submitted.value = false
  },
)

function close(): void {
  emit('update:visible', false)
}

function confirm(): void {
  submitted.value = true
  if (!password.value) return
  emit('submit', password.value)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('features.manageUsers.resetTwoFactor.header')"
    width="min(460px, 92vw)"
    :close-on-click-modal="false"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="confirm">
      <p class="form__message">
        {{ $t('features.manageUsers.resetTwoFactor.message', { fullName: targetName }) }}
      </p>
      <div class="form__field">
        <label for="reset-two-factor-password">{{
          $t('features.manageUsers.resetTwoFactor.passwordLabel')
        }}</label>
        <el-input
          id="reset-two-factor-password"
          v-model="password"
          type="password"
          show-password
          autocomplete="current-password"
          :maxlength="128"
          :class="{ 'ca-invalid': missing || error }"
        />
        <small v-if="missing" class="form__error">{{
          $t('features.manageUsers.resetTwoFactor.passwordRequired')
        }}</small>
        <small v-else-if="error" class="form__error">{{ error }}</small>
      </div>
    </form>

    <template #footer>
      <Button :label="$t('common.cancel')" text :disabled="saving" @click="close" />
      <Button
        :label="$t('features.manageUsers.resetTwoFactor.confirm')"
        type="primary"
        :loading="saving"
        @click="confirm"
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

.form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}
</style>
