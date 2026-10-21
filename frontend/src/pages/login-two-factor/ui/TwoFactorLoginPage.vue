<script setup lang="ts">
import { useTwoFactorLogin } from '../model/use-two-factor-login'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

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
        <StatusPanel v-if="isLoading" state="pending" :text="$t('pages.loginTwoFactor.loading')" />

        <StatusPanel
          v-else-if="expired"
          state="error"
          :title="$t('pages.loginTwoFactor.expiredTitle')"
          :text="$t('pages.loginTwoFactor.expiredText')"
        >
          <template #actions>
            <BrandButton :to="loginRoute" variant="primary">
              {{ $t('pages.loginTwoFactor.backToLogin') }}
            </BrandButton>
          </template>
        </StatusPanel>

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

          <div class="ca-field">
            <label class="ca-field__label" for="two-factor-code">{{
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

          <p v-if="errorMessage" class="ca-form-alert" role="alert">
            {{ errorMessage }}
          </p>

          <BrandButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('pages.loginTwoFactor.submit') }}
          </BrandButton>

          <p v-if="method === 'Email'" class="two-factor-resend">
            {{ $t('pages.loginTwoFactor.resendPrompt') }}
            <BrandButton
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
            </BrandButton>
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

.two-factor-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
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

.two-factor-input :deep(.el-input__inner) {
  font-family: var(--ca-font-mono);
  font-size: 22px;
  letter-spacing: 0.35em;
  text-align: center;
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
