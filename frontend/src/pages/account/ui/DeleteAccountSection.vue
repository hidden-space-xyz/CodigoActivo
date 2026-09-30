<script setup lang="ts">
import { BrandButton } from '@/shared/ui/brand-button'

import { useDeleteAccount } from '../model/use-delete-account'
import { useDeleteAccountDialog } from '../model/use-delete-account-dialog'

const deletion = useDeleteAccount()
const { isAuthenticator, email, canDelete, isInitialAdmin, errorMessage, requestCode, confirm } =
  deletion
const {
  visible,
  codeStep,
  form: { draft, errors },
  canConfirm,
  open,
  close,
  submit,
  resendCode,
} = useDeleteAccountDialog(deletion)
</script>

<template>
  <section class="acc-pane acc-danger">
    <div class="acc-pane__heading">
      <h2 class="acc-danger__title">{{ $t('pages.account.deleteAccount.title') }}</h2>
      <p class="acc-danger__lead">{{ $t('pages.account.deleteAccount.lead') }}</p>
    </div>

    <p v-if="isInitialAdmin" class="acc-danger__note">
      {{ $t('pages.account.deleteAccount.initialAdminNote') }}
    </p>
    <div v-else-if="canDelete" class="acc-danger__actions">
      <BrandButton variant="ghost" class="acc-danger__trigger" @click="open">
        {{ $t('pages.account.deleteAccount.action') }}
      </BrandButton>
    </div>

    <el-dialog
      :model-value="visible"
      :title="$t('pages.account.deleteAccount.dialogHeader')"
      width="min(90vw, 480px)"
      :close-on-click-modal="false"
      @update:model-value="close"
    >
      <form class="acc-form" @submit.prevent="submit">
        <template v-if="!codeStep">
          <p class="acc-form__intro">{{ $t('pages.account.deleteAccount.passwordIntro') }}</p>
          <div class="acc-form__field">
            <label for="del-password">{{ $t('pages.account.deleteAccount.passwordLabel') }}</label>
            <el-input
              id="del-password"
              v-model="draft.password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="128"
            />
            <small v-if="errors.password" class="acc-form__error">{{ errors.password }}</small>
          </div>
        </template>

        <template v-else>
          <p class="acc-form__intro">
            {{
              isAuthenticator
                ? $t('pages.account.deleteAccount.authenticatorIntro')
                : $t('pages.account.deleteAccount.codeSent', { email })
            }}
          </p>
          <div class="acc-form__field">
            <label for="del-code">{{ $t('pages.account.deleteAccount.codeLabel') }}</label>
            <el-input
              id="del-code"
              v-model="draft.code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="16"
            />
          </div>
          <p v-if="!isAuthenticator" class="acc-danger__resend">
            {{ $t('pages.account.deleteAccount.resendPrompt') }}
            <BrandButton
              variant="link"
              class="acc-danger__resend-button"
              :loading="requestCode.isPending.value"
              @click="resendCode"
            >
              {{ $t('pages.account.deleteAccount.resend') }}
            </BrandButton>
          </p>
          <div class="acc-danger__consent">
            <el-checkbox id="del-accept" v-model="draft.accepted" />
            <label for="del-accept" class="acc-danger__consent-label">{{
              $t('pages.account.deleteAccount.irreversible')
            }}</label>
          </div>
        </template>

        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="close">{{
            $t('common.cancel')
          }}</BrandButton>
          <BrandButton
            v-if="!codeStep"
            variant="primary"
            type="submit"
            :loading="requestCode.isPending.value"
          >
            {{
              isAuthenticator
                ? $t('pages.account.deleteAccount.continue')
                : $t('pages.account.deleteAccount.sendCode')
            }}
          </BrandButton>
          <BrandButton
            v-else
            variant="primary"
            type="submit"
            class="acc-danger__submit"
            :disabled="!canConfirm"
            :loading="confirm.isPending.value"
          >
            {{ $t('pages.account.deleteAccount.submit') }}
          </BrandButton>
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
