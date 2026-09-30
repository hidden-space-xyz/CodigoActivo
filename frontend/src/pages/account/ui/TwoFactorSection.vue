<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { BrandButton } from '@/shared/ui/brand-button'

import { useAuthenticatorSetupDialog } from '../model/use-authenticator-setup-dialog'
import { useDisableAuthenticatorDialog } from '../model/use-disable-authenticator-dialog'
import { useTwoFactor } from '../model/use-two-factor'

const { t } = useI18n()
const twoFactor = useTwoFactor()
const { isAuthenticator, setup, qrCodeUrl, errorMessage, beginSetup, confirmSetup, disable } =
  twoFactor

const methodLabel = computed(() =>
  isAuthenticator.value
    ? t('pages.account.twoFactor.methods.Authenticator')
    : t('pages.account.twoFactor.methods.Email'),
)

const {
  visible: setupVisible,
  step: setupStep,
  passwordForm: { draft: setupPassword, errors: setupPasswordErrors },
  codeForm: { draft: setupCode, errors: setupCodeErrors },
  open: openSetup,
  close: closeSetup,
  submit: submitSetup,
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
  <section class="acc-pane">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('pages.account.twoFactor.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('pages.account.twoFactor.lead') }}</p>
      </div>
      <div class="acc-pane__actions">
        <BrandButton v-if="isAuthenticator" variant="ghost" @click="openDisable">
          {{ $t('pages.account.twoFactor.useEmail') }}
        </BrandButton>
        <BrandButton v-else variant="primary" @click="openSetup">
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
      :title="$t('pages.account.twoFactor.setupHeader')"
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
      @update:model-value="closeSetup"
    >
      <form class="acc-form" @submit.prevent="submitSetup">
        <template v-if="setupStep === 'password'">
          <p class="acc-form__intro">{{ $t('pages.account.twoFactor.setupPasswordIntro') }}</p>
          <div class="acc-form__field">
            <label for="tf-setup-password">{{ $t('pages.account.twoFactor.passwordLabel') }}</label>
            <el-input
              id="tf-setup-password"
              v-model="setupPassword.password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="128"
            />
            <small v-if="setupPasswordErrors.password" class="acc-form__error">{{
              setupPasswordErrors.password
            }}</small>
          </div>
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
          <div class="acc-form__field">
            <label for="tf-setup-code">{{ $t('pages.account.twoFactor.codeLabel') }}</label>
            <el-input
              id="tf-setup-code"
              v-model="setupCode.code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="16"
            />
            <small v-if="setupCodeErrors.code" class="acc-form__error">{{
              setupCodeErrors.code
            }}</small>
          </div>
        </template>

        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="closeSetup">{{
            $t('common.cancel')
          }}</BrandButton>
          <BrandButton
            variant="primary"
            type="submit"
            :loading="beginSetup.isPending.value || confirmSetup.isPending.value"
          >
            {{
              setupStep === 'password'
                ? $t('pages.account.twoFactor.continue')
                : $t('pages.account.twoFactor.confirm')
            }}
          </BrandButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      :model-value="disableVisible"
      :title="$t('pages.account.twoFactor.disableHeader')"
      width="min(90vw, 460px)"
      :close-on-click-modal="false"
      @update:model-value="closeDisable"
    >
      <form class="acc-form" @submit.prevent="submitDisable">
        <p class="acc-form__intro">{{ $t('pages.account.twoFactor.disableIntro') }}</p>
        <div class="acc-form__field">
          <label for="tf-disable-password">{{ $t('pages.account.twoFactor.passwordLabel') }}</label>
          <el-input
            id="tf-disable-password"
            v-model="disableDraft.password"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="128"
          />
          <small v-if="disableErrors.password" class="acc-form__error">{{
            disableErrors.password
          }}</small>
        </div>
        <div class="acc-form__field">
          <label for="tf-disable-code">{{ $t('pages.account.twoFactor.codeLabel') }}</label>
          <el-input
            id="tf-disable-code"
            v-model="disableDraft.code"
            inputmode="numeric"
            autocomplete="one-time-code"
            :maxlength="16"
          />
          <small v-if="disableErrors.code" class="acc-form__error">{{ disableErrors.code }}</small>
        </div>
        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="closeDisable">{{
            $t('common.cancel')
          }}</BrandButton>
          <BrandButton variant="primary" type="submit" :loading="disable.isPending.value">
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

.acc-pane__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.acc-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px 24px;
  margin: 0;
}

.acc-info__row {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-info__row dt {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.acc-info__row dd {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
  overflow-wrap: anywhere;
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
  background: #fff;
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

@media (max-width: 640px) {
  .acc-info {
    grid-template-columns: 1fr;
  }

  .acc-pane__actions {
    width: 100%;
  }

  .acc-pane__actions > * {
    flex: 1 1 100%;
  }
}
</style>
