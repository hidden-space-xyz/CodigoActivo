<script setup lang="ts">
import InputText from 'primevue/inputtext'

import { useForgotPassword } from '@/features/auth'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const { form, sent, submit, isSubmitting, isError } = useForgotPassword()
</script>

<template>
  <div>
    <section class="forgot-head">
      <div class="forgot-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow forgot-head__inner">
        <SectionEyebrow :text="$t('pages.forgotPassword.eyebrow')" color="var(--ca-orange-ink)" />
        <h1 class="forgot-head__title">{{ $t('pages.forgotPassword.title') }}</h1>
        <p class="forgot-head__intro">
          {{ $t('pages.forgotPassword.intro') }}
        </p>
      </div>
    </section>

    <section class="forgot-body">
      <div class="forgot-card">
        <form v-if="!sent" class="forgot-form" @submit.prevent="submit">
          <div class="forgot-field">
            <label class="forgot-label" for="forgot-email">{{ $t('common.emailLong') }}</label>
            <InputText
              id="forgot-email"
              v-model="form.email"
              type="email"
              :maxlength="256"
              required
              fluid
            />
          </div>

          <p v-if="isError" class="forgot-error" role="alert">
            {{ $t('pages.forgotPassword.error') }}
          </p>

          <BaseButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('pages.forgotPassword.submit') }}
          </BaseButton>
        </form>

        <div v-else class="forgot-sent" aria-live="polite">
          <div class="forgot-sent__icon" aria-hidden="true">✓</div>
          <h2 class="forgot-sent__title">{{ $t('pages.forgotPassword.sentTitle') }}</h2>
          <p class="forgot-sent__text">
            {{ $t('pages.forgotPassword.sentBody') }}
            {{ $t('pages.forgotPassword.sentExpiry', { minutes: 15 }) }}
          </p>
        </div>

        <p class="forgot-alt">
          {{ $t('pages.forgotPassword.rememberedAlt') }}
          <RouterLink :to="{ name: 'login' }" class="forgot-alt__link"> {{ $t('common.login') }} </RouterLink>
        </p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.forgot-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.forgot-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.forgot-head__inner {
  position: relative;
}

.forgot-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.forgot-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.forgot-body {
  padding: 24px 24px 80px;
}

.forgot-card {
  max-width: 440px;
  margin: 0 auto;
}

.forgot-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.forgot-field {
  display: flex;
  flex-direction: column;
}

.forgot-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 6px;
}

.forgot-error {
  font-size: 13.5px;
  color: var(--ca-danger-ink);
}

.forgot-sent {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 36px 30px;
  text-align: center;
}

.forgot-sent__icon {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  color: var(--ca-success);
}

.forgot-sent__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.forgot-sent__text {
  margin-top: 10px;
  font-size: 15.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.forgot-alt {
  text-align: center;
  margin-top: 18px;
  font-size: 14.5px;
  color: var(--ca-text-muted);
}

.forgot-alt__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
}

.forgot-alt__link:hover {
  text-decoration: underline;
}
</style>
