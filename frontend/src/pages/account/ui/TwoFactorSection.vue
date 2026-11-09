<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { PASSWORD_MAX_LENGTH, TWO_FACTOR_CODE_MAX_LENGTH } from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'
import { FormField } from '@/shared/ui/form-field'

import { useAuthenticatorSetupDialog } from '../model/use-authenticator-setup-dialog'
import { useDisableAuthenticatorDialog } from '../model/use-disable-authenticator-dialog'
import { useTwoFactor } from '../model/use-two-factor'

const { t } = useI18n()
const twoFactor = useTwoFactor()
const { isAuthenticator, email, setup, qrCodeUrl, errorMessage, requestSetupCode, disable } =
  twoFactor

const methodLabel = computed(() =>
  isAuthenticator.value
    ? t('pages.account.twoFactor.methods.Authenticator')
    : t('pages.account.twoFactor.methods.Email'),
)

const {
  visible: setupVisible,
  step: setupStep,
  submitting: setupSubmitting,
  passwordForm: { draft: setupPassword, errors: setupPasswordErrors },
  emailCodeForm: { draft: setupEmailCode, errors: setupEmailCodeErrors },
  codeForm: { draft: setupCode, errors: setupCodeErrors },
  open: openSetup,
  close: closeSetup,
  submit: submitSetup,
  resendCode: resendSetupCode,
} = useAuthenticatorSetupDialog(twoFactor)

const {
  visible: disableVisible,
  form: { draft: disableDraft, errors: disableErrors },
  open: openDisable,
  close: closeDisable,
  submit: submitDisable,
} = useDisableAuthenticatorDialog(twoFactor)
</script>

<template>
  <section class="acc-pane" data-testid="two-factor-section">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('pages.account.twoFactor.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('pages.account.twoFactor.lead') }}</p>
      </div>
      <div class="acc-pane__actions">
        <BrandButton
          v-if="isAuthenticator"
          variant="ghost"
          data-testid="two-factor-use-email"
          @click="openDisable"
        >
          {{ $t('pages.account.twoFactor.useEmail') }}
        </BrandButton>
        <BrandButton
          v-else
          variant="primary"
          data-testid="two-factor-use-authenticator"
          @click="openSetup"
        >
          {{ $t('pages.account.twoFactor.useAuthenticator') }}
        </BrandButton>
      </div>
    </div>

    <dl class="acc-info">
      <div class="acc-info__row">
        <dt>{{ $t('pages.account.twoFactor.methodLabel') }}</dt>
        <dd data-testid="two-factor-method">{{ methodLabel }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('pages.account.twoFactor.securityLabel') }}</dt>
        <dd>
          {{
            isAuthenticator
              ? $t('pages.account.twoFactor.securityAuthenticator')
              : $t('pages.account.twoFactor.securityEmail')
          }}
        </dd>
      </div>
    </dl>

    <el-dialog
      :model-value="setupVisible"
      data-testid="two-factor-setup-dialog"
      :title="$t('pages.account.twoFactor.setupHeader')"
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
      @update:model-value="closeSetup"
    >
      <form class="acc-form" @submit.prevent="submitSetup">
        <template v-if="setupStep === 'password'">
          <p class="acc-form__intro">{{ $t('pages.account.twoFactor.setupPasswordIntro') }}</p>
          <FormField
            v-slot="{ control }"
            for="tf-setup-password"
            :label="$t('common.yourPassword')"
            :error="setupPasswordErrors.password"
            error-testid="two-factor-setup-password-error"
            class="acc-form__field"
          >
            <el-input
              v-model="setupPassword.password"
              v-bind="control"
              data-testid="two-factor-setup-password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="PASSWORD_MAX_LENGTH"
            />
          </FormField>
        </template>

        <template v-else-if="setupStep === 'emailCode'">
          <p class="acc-form__intro" data-testid="two-factor-setup-code-sent">
            {{ $t('pages.account.twoFactor.setupCodeSent', { email }) }}
          </p>
          <FormField
            v-slot="{ control }"
            for="tf-setup-email-code"
            :label="$t('pages.account.twoFactor.emailCodeLabel')"
            :error="setupEmailCodeErrors.code"
            error-testid="two-factor-setup-email-code-error"
            class="acc-form__field"
          >
            <el-input
              v-model="setupEmailCode.code"
              v-bind="control"
              data-testid="two-factor-setup-email-code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
            />
          </FormField>
          <p class="tf-resend">
            {{ $t('common.resendPrompt') }}
            <BrandButton
              variant="link"
              class="tf-resend__button"
              data-testid="two-factor-setup-resend"
              :loading="requestSetupCode.isPending.value"
              @click="resendSetupCode"
            >
              {{ $t('common.resendCode') }}
            </BrandButton>
          </p>
        </template>

        <template v-else-if="setup">
          <p class="acc-form__intro">{{ $t('pages.account.twoFactor.scanIntro') }}</p>
          <div class="tf-enroll">
            <img
              v-if="qrCodeUrl"
              :src="qrCodeUrl"
              :alt="$t('pages.account.twoFactor.qrAlt')"
              class="tf-enroll__qr"
              width="200"
              height="200"
            />
            <div class="tf-enroll__key">
              <span class="tf-enroll__key-label">{{
                $t('pages.account.twoFactor.manualKeyLabel')
              }}</span>
              <code class="tf-enroll__key-value" data-testid="two-factor-shared-key">{{
                setup.sharedKey
              }}</code>
            </div>
          </div>
          <p class="acc-form__intro">{{ $t('pages.account.twoFactor.codeIntro') }}</p>
          <FormField
            v-slot="{ control }"
            for="tf-setup-code"
            :label="$t('pages.account.twoFactor.codeLabel')"
            :error="setupCodeErrors.code"
            error-testid="two-factor-setup-code-error"
            class="acc-form__field"
          >
            <el-input
              v-model="setupCode.code"
              v-bind="control"
              data-testid="two-factor-setup-code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
            />
          </FormField>
        </template>

        <p
          v-if="errorMessage"
          class="ca-form-alert acc-form__alert"
          role="alert"
          data-testid="two-factor-setup-error"
        >
          {{ errorMessage }}
        </p>
        <div class="acc-form__actions">
          <BrandButton
            variant="link"
            type="button"
            data-testid="two-factor-setup-cancel"
            @click="closeSetup"
            >{{ $t('common.cancel') }}</BrandButton
          >
          <BrandButton
            v-if="setupStep === 'password'"
            variant="primary"
            type="submit"
            data-testid="two-factor-setup-send-code"
            :loading="setupSubmitting"
          >
            {{ $t('common.sendCode') }}
          </BrandButton>
          <BrandButton
            v-else-if="setupStep === 'emailCode'"
            variant="primary"
            type="submit"
            data-testid="two-factor-setup-continue"
            :loading="setupSubmitting"
          >
            {{ $t('pages.account.twoFactor.continue') }}
          </BrandButton>
          <BrandButton
            v-else
            variant="primary"
            type="submit"
            data-testid="two-factor-setup-activate"
            :loading="setupSubmitting"
          >
            {{ $t('pages.account.twoFactor.confirm') }}
          </BrandButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      :model-value="disableVisible"
      data-testid="two-factor-disable-dialog"
      :title="$t('pages.account.twoFactor.disableHeader')"
      width="min(90vw, 460px)"
      :close-on-click-modal="false"
      @update:model-value="closeDisable"
    >
      <form class="acc-form" @submit.prevent="submitDisable">
        <p class="acc-form__intro">{{ $t('pages.account.twoFactor.disableIntro') }}</p>
        <FormField
          v-slot="{ control }"
          for="tf-disable-password"
          :label="$t('common.yourPassword')"
          :error="disableErrors.password"
          error-testid="two-factor-disable-password-error"
          class="acc-form__field"
        >
          <el-input
            v-model="disableDraft.password"
            v-bind="control"
            data-testid="two-factor-disable-password"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="PASSWORD_MAX_LENGTH"
          />
        </FormField>
        <FormField
          v-slot="{ control }"
          for="tf-disable-code"
          :label="$t('pages.account.twoFactor.codeLabel')"
          :error="disableErrors.code"
          error-testid="two-factor-disable-code-error"
          class="acc-form__field"
        >
          <el-input
            v-model="disableDraft.code"
            v-bind="control"
            data-testid="two-factor-disable-code"
            inputmode="numeric"
            autocomplete="one-time-code"
            :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
          />
        </FormField>
        <p
          v-if="errorMessage"
          class="ca-form-alert acc-form__alert"
          role="alert"
          data-testid="two-factor-disable-error"
        >
          {{ errorMessage }}
        </p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="closeDisable">{{
            $t('common.cancel')
          }}</BrandButton>
          <BrandButton
            variant="primary"
            type="submit"
            :loading="disable.isPending.value"
            data-testid="two-factor-disable-submit"
          >
            {{ $t('pages.account.twoFactor.useEmail') }}
          </BrandButton>
        </div>
      </form>
    </el-dialog>
  </section>
</template>

<style scoped>
.acc-pane__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-pane__heading {
  flex: 1;
  min-width: 240px;
}

.acc-pane__title {
  margin: 0 0 6px;
  font-family: var(--ca-font-display);
  font-size: 20px;
  font-weight: 700;
  color: var(--ca-text-bright);
}

.acc-pane__lead {
  margin: 0;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 56ch;
}

.tf-resend {
  margin: 0 0 14px;
  font-size: 13.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.tf-resend__button {
  display: inline;
  padding: 0;
  font-size: inherit;
}

.tf-enroll {
  display: flex;
  align-items: center;
  gap: 18px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}

.tf-enroll__qr {
  width: 200px;
  height: 200px;
  border-radius: 12px;
  background: var(--ca-paper);
  padding: 6px;
  border: 1px solid var(--ca-border-strong);
}

.tf-enroll__key {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  flex: 1;
}

.tf-enroll__key-label {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.tf-enroll__key-value {
  font-family: var(--ca-font-mono);
  font-size: 14px;
  line-height: 1.6;
  color: var(--ca-text);
  overflow-wrap: anywhere;
  user-select: all;
}
</style>
