<script setup lang="ts">
import { PASSWORD_MAX_LENGTH, TWO_FACTOR_CODE_MAX_LENGTH } from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'
import { FormField } from '@/shared/ui/form-field'

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
  <section class="acc-pane acc-danger" data-testid="delete-account-section">
    <div class="acc-pane__heading">
      <h2 class="acc-danger__title">{{ $t('pages.account.deleteAccount.title') }}</h2>
      <p class="acc-danger__lead">{{ $t('pages.account.deleteAccount.lead') }}</p>
    </div>

    <p
      v-if="isInitialAdmin"
      class="acc-danger__note"
      data-testid="delete-account-initial-admin-note"
    >
      {{ $t('pages.account.deleteAccount.initialAdminNote') }}
    </p>
    <div v-else-if="canDelete" class="acc-danger__actions">
      <BrandButton
        variant="ghost"
        class="acc-danger__trigger"
        data-testid="delete-account-open"
        @click="open"
      >
        {{ $t('pages.account.deleteAccount.action') }}
      </BrandButton>
    </div>

    <el-dialog
      :model-value="visible"
      data-testid="delete-account-dialog"
      :title="$t('pages.account.deleteAccount.dialogHeader')"
      width="min(90vw, 480px)"
      :close-on-click-modal="false"
      @update:model-value="close"
    >
      <form class="acc-form" @submit.prevent="submit">
        <template v-if="!codeStep">
          <p class="acc-form__intro">{{ $t('pages.account.deleteAccount.passwordIntro') }}</p>
          <FormField
            v-slot="{ control }"
            for="del-password"
            :label="$t('common.yourPassword')"
            :error="errors.password"
            error-testid="delete-account-password-error"
            class="acc-form__field"
          >
            <el-input
              v-model="draft.password"
              v-bind="control"
              data-testid="delete-account-password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="PASSWORD_MAX_LENGTH"
            />
          </FormField>
        </template>

        <template v-else>
          <p class="acc-form__intro" data-testid="delete-account-code-intro">
            {{
              isAuthenticator
                ? $t('pages.account.deleteAccount.authenticatorIntro')
                : $t('pages.account.deleteAccount.codeSent', { email })
            }}
          </p>
          <FormField
            for="del-code"
            :label="$t('pages.account.deleteAccount.codeLabel')"
            class="acc-form__field"
          >
            <el-input
              id="del-code"
              v-model="draft.code"
              data-testid="delete-account-code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
            />
          </FormField>
          <p v-if="!isAuthenticator" class="acc-danger__resend">
            {{ $t('common.resendPrompt') }}
            <BrandButton
              variant="link"
              class="acc-danger__resend-button"
              :loading="requestCode.isPending.value"
              @click="resendCode"
            >
              {{ $t('common.resendCode') }}
            </BrandButton>
          </p>
          <div class="acc-danger__consent">
            <el-checkbox
              id="del-accept"
              v-model="draft.accepted"
              data-testid="delete-account-irreversible"
              class="ca-checkbox-text acc-danger__consent-label"
            >
              {{ $t('pages.account.deleteAccount.irreversible') }}
            </el-checkbox>
          </div>
        </template>

        <p
          v-if="errorMessage"
          class="ca-form-alert acc-form__alert"
          role="alert"
          data-testid="delete-account-error"
        >
          {{ errorMessage }}
        </p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="close">{{
            $t('common.cancel')
          }}</BrandButton>
          <BrandButton
            v-if="!codeStep"
            variant="primary"
            type="submit"
            data-testid="delete-account-send-code"
            :loading="requestCode.isPending.value"
          >
            {{
              isAuthenticator ? $t('pages.account.deleteAccount.continue') : $t('common.sendCode')
            }}
          </BrandButton>
          <BrandButton
            v-else
            variant="primary"
            type="submit"
            class="acc-danger__submit"
            data-testid="delete-account-submit"
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

@media (max-width: 640px) {
  .acc-danger__actions {
    width: 100%;
  }

  .acc-danger__actions > * {
    flex: 1 1 100%;
  }
}
</style>
