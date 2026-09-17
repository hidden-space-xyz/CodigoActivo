<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useTwoFactor } from '../model/useTwoFactor'
import { BaseButton } from '@/shared/ui'

const { t } = useI18n()
const {
  isAuthenticator,
  setup,
  qrCodeUrl,
  errorMessage,
  reset,
  beginSetup,
  confirmSetup,
  disable,
} = useTwoFactor()

const methodLabel = computed(() =>
  isAuthenticator.value
    ? t('features.account.twoFactor.methods.Authenticator')
    : t('features.account.twoFactor.methods.Email'),
)

const setupVisible = ref(false)
const setupForm = reactive({ password: '', code: '' })
const setupSubmitted = ref(false)
const setupStep = computed(() => (setup.value ? 'confirm' : 'password'))

function openSetup(): void {
  reset()
  setupForm.password = ''
  setupForm.code = ''
  setupSubmitted.value = false
  setupVisible.value = true
}

function closeSetup(): void {
  setupVisible.value = false
  reset()
}

function submitSetup(): void {
  setupSubmitted.value = true
  if (setupStep.value === 'password') {
    if (!setupForm.password) return
    beginSetup.mutate(setupForm.password, {
      onSuccess: () => {
        setupSubmitted.value = false
      },
    })
    return
  }
  const code = setupForm.code.trim()
  if (!code) return
  confirmSetup.mutate(code, {
    onSuccess: () => {
      setupVisible.value = false
    },
  })
}

const disableVisible = ref(false)
const disableForm = reactive({ password: '', code: '' })
const disableSubmitted = ref(false)

function openDisable(): void {
  reset()
  disableForm.password = ''
  disableForm.code = ''
  disableSubmitted.value = false
  disableVisible.value = true
}

function closeDisable(): void {
  disableVisible.value = false
  reset()
}

function submitDisable(): void {
  disableSubmitted.value = true
  const code = disableForm.code.trim()
  if (!disableForm.password || !code) return
  disable.mutate(
    { currentPassword: disableForm.password, code },
    {
      onSuccess: () => {
        disableVisible.value = false
      },
    },
  )
}
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('features.account.twoFactor.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('features.account.twoFactor.lead') }}</p>
      </div>
      <div class="acc-pane__actions">
        <BaseButton v-if="isAuthenticator" variant="ghost" @click="openDisable">
          {{ $t('features.account.twoFactor.useEmail') }}
        </BaseButton>
        <BaseButton v-else variant="primary" @click="openSetup">
          {{ $t('features.account.twoFactor.useAuthenticator') }}
        </BaseButton>
      </div>
    </div>

    <dl class="acc-info">
      <div class="acc-info__row">
        <dt>{{ $t('features.account.twoFactor.methodLabel') }}</dt>
        <dd data-testid="two-factor-method">{{ methodLabel }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('features.account.twoFactor.securityLabel') }}</dt>
        <dd>
          {{
            isAuthenticator
              ? $t('features.account.twoFactor.securityAuthenticator')
              : $t('features.account.twoFactor.securityEmail')
          }}
        </dd>
      </div>
    </dl>

    <el-dialog
      :model-value="setupVisible"
      :title="$t('features.account.twoFactor.setupHeader')"
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
      @update:model-value="closeSetup"
    >
      <form class="acc-form" @submit.prevent="submitSetup">
        <template v-if="setupStep === 'password'">
          <p class="acc-form__intro">{{ $t('features.account.twoFactor.setupPasswordIntro') }}</p>
          <div class="acc-form__field">
            <label for="tf-setup-password">{{
              $t('features.account.twoFactor.passwordLabel')
            }}</label>
            <el-input
              id="tf-setup-password"
              v-model="setupForm.password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="128"
            />
            <small v-if="setupSubmitted && !setupForm.password" class="acc-form__error">{{
              $t('features.account.twoFactor.passwordRequired')
            }}</small>
          </div>
        </template>

        <template v-else-if="setup">
          <p class="acc-form__intro">{{ $t('features.account.twoFactor.scanIntro') }}</p>
          <div class="tf-enroll">
            <img
              v-if="qrCodeUrl"
              :src="qrCodeUrl"
              :alt="$t('features.account.twoFactor.qrAlt')"
              class="tf-enroll__qr"
              width="200"
              height="200"
            />
            <div class="tf-enroll__key">
              <span class="tf-enroll__key-label">{{
                $t('features.account.twoFactor.manualKeyLabel')
              }}</span>
              <code class="tf-enroll__key-value" data-testid="two-factor-shared-key">{{
                setup.sharedKey
              }}</code>
            </div>
          </div>
          <p class="acc-form__intro">{{ $t('features.account.twoFactor.codeIntro') }}</p>
          <div class="acc-form__field">
            <label for="tf-setup-code">{{ $t('features.account.twoFactor.codeLabel') }}</label>
            <el-input
              id="tf-setup-code"
              v-model="setupForm.code"
              inputmode="numeric"
              autocomplete="one-time-code"
              :maxlength="16"
            />
            <small v-if="setupSubmitted && !setupForm.code.trim()" class="acc-form__error">{{
              $t('features.account.twoFactor.codeRequired')
            }}</small>
          </div>
        </template>

        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="closeSetup">{{
            $t('common.cancel')
          }}</BaseButton>
          <BaseButton
            variant="primary"
            type="submit"
            :loading="beginSetup.isPending.value || confirmSetup.isPending.value"
          >
            {{
              setupStep === 'password'
                ? $t('features.account.twoFactor.continue')
                : $t('features.account.twoFactor.confirm')
            }}
          </BaseButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      :model-value="disableVisible"
      :title="$t('features.account.twoFactor.disableHeader')"
      width="min(90vw, 460px)"
      :close-on-click-modal="false"
      @update:model-value="closeDisable"
    >
      <form class="acc-form" @submit.prevent="submitDisable">
        <p class="acc-form__intro">{{ $t('features.account.twoFactor.disableIntro') }}</p>
        <div class="acc-form__field">
          <label for="tf-disable-password">{{
            $t('features.account.twoFactor.passwordLabel')
          }}</label>
          <el-input
            id="tf-disable-password"
            v-model="disableForm.password"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="128"
          />
          <small v-if="disableSubmitted && !disableForm.password" class="acc-form__error">{{
            $t('features.account.twoFactor.passwordRequired')
          }}</small>
        </div>
        <div class="acc-form__field">
          <label for="tf-disable-code">{{ $t('features.account.twoFactor.codeLabel') }}</label>
          <el-input
            id="tf-disable-code"
            v-model="disableForm.code"
            inputmode="numeric"
            autocomplete="one-time-code"
            :maxlength="16"
          />
          <small v-if="disableSubmitted && !disableForm.code.trim()" class="acc-form__error">{{
            $t('features.account.twoFactor.codeRequired')
          }}</small>
        </div>
        <p v-if="errorMessage" class="acc-form__error" role="alert">{{ errorMessage }}</p>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="closeDisable">{{
            $t('common.cancel')
          }}</BaseButton>
          <BaseButton variant="primary" type="submit" :loading="disable.isPending.value">
            {{ $t('features.account.twoFactor.useEmail') }}
          </BaseButton>
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
