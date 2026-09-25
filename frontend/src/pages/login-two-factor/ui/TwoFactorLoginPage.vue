<script setup lang="ts">
import { useTwoFactorLogin } from '@/features/auth'
import { AppIcon, BaseButton, PageHead } from '@/shared/ui'

const {
  form,
  method,
  maskedEmail,
  expired,
  errorMessage,
  loginRoute,
  submit,
  resendCode,
  resendCooldown,
  isLoading,
  isSubmitting,
  isResending,
} = useTwoFactorLogin()
</script>

<template>
  <div>
    <PageHead
      :eyebrow="$t('pages.loginTwoFactor.eyebrow')"
      :title="$t('pages.loginTwoFactor.title')"
    >
      <p class="two-factor-head__intro">
        {{ $t('pages.loginTwoFactor.intro') }}
      </p>
    </PageHead>

    <section class="two-factor-body">
      <div class="two-factor-card">
        <div v-if="isLoading" class="two-factor-panel two-factor-panel--loading">
          <span class="two-factor-panel__icon" aria-hidden="true">
            <AppIcon name="spinner" spin />
          </span>
          <p class="two-factor-panel__text" aria-live="polite">
            {{ $t('pages.loginTwoFactor.loading') }}
          </p>
        </div>

        <div v-else-if="expired" class="two-factor-panel">
          <div class="two-factor-panel__icon two-factor-panel__icon--error" aria-hidden="true">
            !
          </div>
          <h2 class="two-factor-panel__title">{{ $t('pages.loginTwoFactor.expiredTitle') }}</h2>
          <p class="two-factor-panel__text" role="alert">
            {{ $t('pages.loginTwoFactor.expiredText') }}
          </p>
          <div class="two-factor-panel__actions">
            <BaseButton :to="loginRoute" variant="primary">
              {{ $t('pages.loginTwoFactor.backToLogin') }}
            </BaseButton>
          </div>
        </div>

        <form v-else class="two-factor-form" @submit.prevent="submit">
          <p class="two-factor-form__intro">
            <template v-if="method === 'Authenticator'">
              {{ $t('pages.loginTwoFactor.authenticatorIntro') }}
            </template>
            <template v-else>
              {{ $t('pages.loginTwoFactor.emailIntroBefore') }}
              <b>{{ maskedEmail }}</b
              >{{ $t('pages.loginTwoFactor.emailIntroAfter') }}
            </template>
          </p>

          <div class="two-factor-field">
            <label class="two-factor-label" for="two-factor-code">{{
              $t('pages.loginTwoFactor.codeLabel')
            }}</label>
            <el-input
              id="two-factor-code"
              v-model="form.code"
              class="two-factor-input"
              inputmode="numeric"
              autocomplete="one-time-code"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              :maxlength="16"
              required
            />
          </div>

          <div class="two-factor-keep">
            <el-checkbox id="two-factor-keep-signed-in" v-model="form.keepSignedIn" />
            <div class="two-factor-keep__text">
              <label for="two-factor-keep-signed-in" class="two-factor-keep__label">{{
                $t('pages.loginTwoFactor.keepSignedIn')
              }}</label>
              <i18n-t
                keypath="pages.loginTwoFactor.keepSignedInHint"
                tag="p"
                class="two-factor-keep__hint"
              >
                <template #policy>
                  <RouterLink v-slot="{ href }" :to="{ name: 'cookie-policy' }" custom>
                    <a :href="href" target="_blank" rel="noopener" class="two-factor-keep__link">{{
                      $t('pages.loginTwoFactor.keepSignedInPolicy')
                    }}</a>
                  </RouterLink>
                </template>
              </i18n-t>
            </div>
          </div>

          <p v-if="errorMessage" class="two-factor-error" role="alert">
            {{ errorMessage }}
          </p>

          <BaseButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('pages.loginTwoFactor.submit') }}
          </BaseButton>

          <p v-if="method === 'Email'" class="two-factor-resend">
            {{ $t('pages.loginTwoFactor.resendPrompt') }}
            <BaseButton
              variant="link"
              class="two-factor-resend__button"
              :disabled="resendCooldown > 0 || isResending"
              :loading="isResending"
              @click="resendCode"
            >
              {{
                resendCooldown > 0
                  ? $t('pages.loginTwoFactor.resendCountdown', { s: resendCooldown })
                  : $t('pages.loginTwoFactor.resend')
              }}
            </BaseButton>
          </p>
        </form>

        <p v-if="!isLoading && !expired" class="two-factor-alt">
          <RouterLink :to="loginRoute" class="two-factor-alt__link">
            {{ $t('pages.loginTwoFactor.backToLogin') }}
          </RouterLink>
        </p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.two-factor-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.two-factor-body {
  padding: 24px var(--ca-gutter) 80px;
}

.two-factor-card {
  max-width: 440px;
  margin: 0 auto;
}

.two-factor-form,
.two-factor-panel {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
}

.two-factor-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.two-factor-form__intro {
  margin: 0;
  font-size: 14.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.two-factor-field {
  display: flex;
  flex-direction: column;
}

.two-factor-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 6px;
}

.two-factor-input :deep(.el-input__inner) {
  font-family: var(--ca-font-mono);
  font-size: 22px;
  letter-spacing: 0.35em;
  text-align: center;
}

.two-factor-keep {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.two-factor-keep__text {
  min-width: 0;
  padding-top: 5px;
}

.two-factor-keep__label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.5;
  color: var(--ca-text);
  cursor: pointer;
}

.two-factor-keep__hint {
  margin: 4px 0 0;
  font-size: 13px;
  line-height: 1.55;
  color: var(--ca-text-muted);
}

.two-factor-keep__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
}

.two-factor-keep__link:hover {
  text-decoration: underline;
}

.two-factor-error {
  margin: 0;
  font-size: 13.5px;
  color: var(--ca-danger-ink);
}

.two-factor-resend {
  margin: 4px 0 0;
  font-size: 13.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  text-align: center;
}

.two-factor-resend__button {
  display: inline;
  padding: 0;
  font-size: inherit;
}

.two-factor-panel {
  text-align: center;
}

.two-factor-panel__icon {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
}

.two-factor-panel__icon--error {
  background: var(--ca-danger-soft);
  border: 1px solid var(--ca-danger);
  color: var(--ca-danger);
  font-weight: 700;
}

.two-factor-panel__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.two-factor-panel__text {
  margin-top: 10px;
  font-size: 15.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.two-factor-panel__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 24px;
  flex-wrap: wrap;
}

.two-factor-alt {
  text-align: center;
  margin-top: 18px;
  font-size: 14.5px;
}

.two-factor-alt__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
}

.two-factor-alt__link:hover {
  text-decoration: underline;
}
</style>
