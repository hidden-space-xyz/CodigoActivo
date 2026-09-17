<script setup lang="ts">
import { computed, reactive, ref } from 'vue'

import { useDeleteAccount } from '../model/useDeleteAccount'
import { useSession } from '@/entities/session'
import { BaseButton } from '@/shared/ui'

const session = useSession()
const { isAuthenticator, email, errorMessage, reset, requestCode, confirm } = useDeleteAccount()

const visible = ref(false)
const codeStep = ref(false)
const submitted = ref(false)
const form = reactive({ password: '', code: '', accepted: false })

const canConfirm = computed(
  () => form.password.length > 0 && form.code.trim().length > 0 && form.accepted,
)

function open(): void {
  reset()
  codeStep.value = false
  submitted.value = false
  form.password = ''
  form.code = ''
  form.accepted = false
  visible.value = true
}

function close(): void {
  visible.value = false
  reset()
}

function submitPassword(): void {
  submitted.value = true
  if (!form.password) return
  if (isAuthenticator.value) {
    submitted.value = false
    codeStep.value = true
    return
  }
  requestCode.mutate(form.password, {
    onSuccess: () => {
      submitted.value = false
      codeStep.value = true
    },
  })
}

function resendCode(): void {
  requestCode.mutate(form.password)
}

function submitDeletion(): void {
  if (!canConfirm.value) return
  confirm.mutate(
    { currentPassword: form.password, code: form.code.trim() },
    {
      onSuccess: () => {
        visible.value = false
      },
    },
  )
}

function submit(): void {
  if (codeStep.value) submitDeletion()
  else submitPassword()
}
</script>

<template>
  <section class="acc-pane acc-danger">
    <div class="acc-pane__heading">
      <h2 class="acc-danger__title">{{ $t('features.account.deleteAccount.title') }}</h2>
      <p class="acc-danger__lead">{{ $t('features.account.deleteAccount.lead') }}</p>
    </div>

    <p v-if="session.isAdmin" class="acc-danger__note">
      {{ $t('features.account.deleteAccount.adminNote') }}
    </p>
    <div v-else class="acc-danger__actions">
      <BaseButton variant="ghost" class="acc-danger__trigger" @click="open">
        {{ $t('features.account.deleteAccount.action') }}
      </BaseButton>
    </div>

    <el-dialog
      :model-value="visible"
      :title="$t('features.account.deleteAccount.dialogHeader')"
      width="min(90vw, 480px)"
      :close-on-click-modal="false"
      @update:model-value="close"
    >
      <form class="acc-form" @submit.prevent="submit">
        <template v-if="!codeStep">
          <p class="acc-form__intro">{{ $t('features.account.deleteAccount.passwordIntro') }}</p>
          <div class="acc-form__field">
            <label for="del-password">{{
              $t('features.account.deleteAccount.passwordLabel')
            }}</label>
            <el-input
              id="del-password"
              v-model="form.password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="128"
            />
            <small v-if="submitted && !form.password" class="acc-form__error">{{
              $t('features.account.deleteAccount.passwordRequired')
            }}</small>
          </div>
        </template>

        <template v-else>
          <p class="acc-form__intro">
            {{
              isAuthenticator
                ? $t('features.account.deleteAccount.authenticatorIntro')
                : $t('features.account.deleteAccount.codeSent', { email })
            }}
          </p>
          <div class="acc-form__field">
            <label for="del-code">{{ $t('features.account.deleteAccount.codeLabel') }}</label>
            <el-input
              id="del-code"
              v-model="form.code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="16"
            />
          </div>
          <p v-if="!isAuthenticator" class="acc-danger__resend">
            {{ $t('features.account.deleteAccount.resendPrompt') }}
            <BaseButton
              variant="link"
              class="acc-danger__resend-button"
              :loading="requestCode.isPending.value"
              @click="resendCode"
            >
              {{ $t('features.account.deleteAccount.resend') }}
            </BaseButton>
          </p>
          <div class="acc-danger__consent">
            <el-checkbox id="del-accept" v-model="form.accepted" />
            <label for="del-accept" class="acc-danger__consent-label">{{
              $t('features.account.deleteAccount.irreversible')
            }}</label>
          </div>
        </template>

        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="close">{{
            $t('common.cancel')
          }}</BaseButton>
          <BaseButton
            v-if="!codeStep"
            variant="primary"
            type="submit"
            :loading="requestCode.isPending.value"
          >
            {{
              isAuthenticator
                ? $t('features.account.deleteAccount.continue')
                : $t('features.account.deleteAccount.sendCode')
            }}
          </BaseButton>
          <BaseButton
            v-else
            variant="primary"
            type="submit"
            class="acc-danger__submit"
            :disabled="!canConfirm"
            :loading="confirm.isPending.value"
          >
            {{ $t('features.account.deleteAccount.submit') }}
          </BaseButton>
        </div>
      </form>
    </el-dialog>
  </section>
</template>

<style scoped>
.acc-danger {
  border: 1px solid var(--ca-danger);
  border-radius: 14px;
  background: var(--ca-danger-soft);
  padding: 20px 22px;
}

.acc-pane__heading {
  margin-bottom: 14px;
}

.acc-danger__title {
  margin: 0 0 6px;
  font-family: var(--ca-font-display);
  font-size: 20px;
  font-weight: 700;
  color: var(--ca-danger-ink);
}

.acc-danger__lead {
  margin: 0;
  font-size: 14px;
  line-height: 1.55;
  color: var(--ca-text-muted);
  max-width: 62ch;
}

.acc-danger__note {
  margin: 0;
  font-size: 13.5px;
  line-height: 1.55;
  color: var(--ca-text-dim);
}

.acc-danger__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.acc-danger__trigger {
  color: var(--ca-danger-ink);
  border-color: var(--ca-danger);
}

.acc-danger__resend {
  margin: 0 0 14px;
  font-size: 13.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.acc-danger__resend-button {
  display: inline;
  padding: 0;
  font-size: inherit;
}

.acc-danger__consent {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  margin-bottom: 14px;
}

.acc-danger__consent-label {
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text);
}

.acc-danger__submit {
  color: var(--ca-on-primary);
  background: var(--ca-danger);
}

.acc-danger__submit:hover {
  background: var(--ca-danger-ink);
}

.acc-form__intro {
  margin: 0 0 14px;
  font-size: 14px;
  line-height: 1.55;
  color: var(--ca-text-muted);
}

.acc-form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 14px;
}

.acc-form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

.acc-form__error {
  color: var(--ca-danger-ink);
  font-size: 13.5px;
  margin: 0 0 10px;
}

@media (max-width: 640px) {
  .acc-danger__actions {
    width: 100%;
  }

  .acc-danger__actions > * {
    flex: 1 1 100%;
  }
}
</style>
